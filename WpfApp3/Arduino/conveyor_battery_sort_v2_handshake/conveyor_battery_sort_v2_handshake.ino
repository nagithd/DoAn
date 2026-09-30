// Handshake version of conveyor_battery_sort_v2.ino.
// Hardware retained from v2: D11 motor PWM, D2 camera sensor, D3 robot sensor.
// PWM 0..170 runs the calibrated conveyor. PWM 255 is STOP.

#define bangtai 11
const int CAM_BIEN_KIM_LOAI_1 = 2;
const int CAM_BIEN_KIM_LOAI_2 = 3;
const int PWM_TOI_DA_DA_HIEU_CHUAN = 170;
const int PWM_DUNG = 255;

unsigned long previousMillis = 0;
unsigned long currentMillis = 0;
int currentPwm = 100;
String hostSession;
String receiveCommand;

enum ConveyorState {
  WAIT_FOR_HELLO,
  STOPPED,
  RUNNING_TO_SENSOR_1,
  RUNNING_TO_CAMERA,
  CAMERA_SETTLE,
  WAITING_FOR_CAMERA_READY,
  WAITING_FOR_SENSOR_2,
  RUNNING_TO_ROBOT,
  WAITING_FOR_ROBOT_DONE,
  CLEARING_CYCLE,
  FAULT
};

ConveyorState state = WAIT_FOR_HELLO;
unsigned long stateStartedAt = 0;
unsigned long lastPingAt = 0;
unsigned long clearStartedAt = 0;
unsigned long cycleId = 0;
bool isPaired = false;
bool sensorsClearStarted = false;

void setMotor(int pwm) {
  analogWrite(bangtai, pwm);
}

void changeState(ConveyorState nextState) {
  state = nextState;
  stateStartedAt = millis();
}

void sendEvent(const char* eventName) {
  Serial.print("EVENT:");
  Serial.print(eventName);
  Serial.print(":");
  Serial.print(hostSession);
  Serial.print(":");
  Serial.println(cycleId);
}

void enterFault(const char* reason) {
  setMotor(PWM_DUNG);
  changeState(FAULT);
  Serial.print("EVENT:FAULT:");
  Serial.println(reason);
}

void stopConveyor() {
  setMotor(PWM_DUNG);
  if (state != FAULT) {
    changeState(STOPPED);
  }
}

bool isHexSession(const String& value) {
  if (value.length() != 16) return false;
  for (unsigned int index = 0; index < value.length(); index++) {
    char c = value[index];
    if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
  }
  return true;
}

bool parseUnsigned(const String& value, unsigned long& result) {
  if (value.length() == 0 || value.length() > 10) return false;
  result = 0;
  for (unsigned int index = 0; index < value.length(); index++) {
    char c = value[index];
    if (c < '0' || c > '9') return false;
    uint8_t digit = c - '0';
    // unsigned long on Arduino Uno is 32-bit.
    if (result > (4294967295UL - digit) / 10UL) return false;
    result = result * 10UL + digit;
  }
  return true;
}

bool parseCycleToken(const String& payload, unsigned long& receivedCycleId) {
  int separator = payload.indexOf(':');
  if (separator < 0) return false;
  if (payload.substring(0, separator) != hostSession) return false;
  if (!parseUnsigned(payload.substring(separator + 1), receivedCycleId)) return false;
  return receivedCycleId != 0 && receivedCycleId == cycleId;
}

unsigned long delayToCameraMs() {
  // Kept from v2: delay = 1382000 / (1767 - PWM).
  return 1382000UL / (1767UL - (unsigned long)currentPwm);
}

unsigned long delayToRobotMs() {
  // Kept from v2 calibration, valid only for PWM <= 170.
  if (currentPwm <= 60) {
    return 1160UL + 2UL * (unsigned long)currentPwm;
  }
  return 1280UL + (unsigned long)(1.2083f * (currentPwm - 60));
}

void beginSession(const String& session) {
  hostSession = session;
  cycleId = 0;
  isPaired = true;
  lastPingAt = millis();
  setMotor(PWM_DUNG);
  changeState(STOPPED);
  Serial.print("ACK:HELLO:");
  Serial.println(hostSession);
}

void processCommand(String command) {
  command.trim();

  if (command.startsWith("HELLO:")) {
    String session = command.substring(6);
    if (!isHexSession(session)) {
      Serial.println("ERR:SESSION");
      return;
    }
    beginSession(session);
    return;
  }

  if (!isPaired) {
    Serial.println("ERR:HELLO_REQUIRED");
    return;
  }

  if (command == "PING:" + hostSession) {
    lastPingAt = millis();
    return;
  }

  if (command.startsWith("motor = ")) {
    unsigned long requestedPwm;
    if (!parseUnsigned(command.substring(8), requestedPwm)) {
      Serial.println("ERR:PWM");
      return;
    }

    if (requestedPwm == PWM_DUNG) {
      stopConveyor();
      Serial.println("ACK:MOTOR_PWM:255");
      return;
    }

    if (requestedPwm > PWM_TOI_DA_DA_HIEU_CHUAN || state == FAULT ||
        !(state == STOPPED || state == RUNNING_TO_SENSOR_1)) {
      Serial.println("ERR:MOTOR_STATE_OR_PWM");
      return;
    }

    currentPwm = (int)requestedPwm;
    setMotor(currentPwm);
    changeState(RUNNING_TO_SENSOR_1);
    lastPingAt = millis();
    Serial.print("ACK:MOTOR_PWM:");
    Serial.println(currentPwm);
    return;
  }

  int commandSeparator = command.indexOf(':');
  if (commandSeparator < 0) {
    Serial.println("ERR:COMMAND");
    return;
  }

  String verb = command.substring(0, commandSeparator);
  unsigned long receivedCycleId;
  if (!parseCycleToken(command.substring(commandSeparator + 1), receivedCycleId)) {
    Serial.println("ERR:CYCLE");
    return;
  }

  if (verb == "CAMERA_READY") {
    if (state == WAITING_FOR_CAMERA_READY) {
      setMotor(currentPwm);
      changeState(WAITING_FOR_SENSOR_2);
      sendEvent("MOVING_TO_ROBOT");
    } else if (!(state == WAITING_FOR_SENSOR_2 || state == RUNNING_TO_ROBOT ||
                 state == WAITING_FOR_ROBOT_DONE)) {
      Serial.println("ERR:CYCLE_STATE");
      return;
    }
    Serial.print("ACK:CAMERA_READY:");
    Serial.print(hostSession);
    Serial.print(":");
    Serial.println(cycleId);
    return;
  }

  if (verb == "ROBOT_DONE") {
    if (state == WAITING_FOR_ROBOT_DONE) {
      setMotor(currentPwm);
      sensorsClearStarted = false;
      changeState(CLEARING_CYCLE);
      sendEvent("CYCLE_COMPLETED");
    } else if (!(state == CLEARING_CYCLE || state == RUNNING_TO_SENSOR_1)) {
      Serial.println("ERR:CYCLE_STATE");
      return;
    }
    Serial.print("ACK:ROBOT_DONE:");
    Serial.print(hostSession);
    Serial.print(":");
    Serial.println(cycleId);
    return;
  }

  Serial.println("ERR:COMMAND");
}

void readSerialWithoutBlocking() {
  for (uint8_t processed = 0; processed < 64 && Serial.available() > 0; processed++) {
    char received = Serial.read();
    if (received == '@') {
      processCommand(receiveCommand);
      receiveCommand = "";
    } else if (received != '\r' && received != '\n') {
      if (receiveCommand.length() < 95) {
        receiveCommand += received;
      } else {
        receiveCommand = "";
        enterFault("SERIAL_OVERFLOW");
      }
    }
  }
}

void setup() {
  pinMode(CAM_BIEN_KIM_LOAI_1, INPUT_PULLUP);
  pinMode(CAM_BIEN_KIM_LOAI_2, INPUT_PULLUP);
  pinMode(bangtai, OUTPUT);
  Serial.begin(9600);
  receiveCommand.reserve(96);
  hostSession.reserve(16);
  setMotor(PWM_DUNG);
  changeState(WAIT_FOR_HELLO);
  Serial.println("READY:CONVEYOR_HANDSHAKE:1");
}

void loop() {
  readSerialWithoutBlocking();

  bool sensor1Detected = digitalRead(CAM_BIEN_KIM_LOAI_1) == LOW;
  bool sensor2Detected = digitalRead(CAM_BIEN_KIM_LOAI_2) == LOW;
  unsigned long now = millis();
  unsigned long elapsed = now - stateStartedAt;

  if (isPaired && state != STOPPED && state != WAIT_FOR_HELLO && state != FAULT &&
      now - lastPingAt > 5000UL) {
    enterFault("HOST_TIMEOUT");
  }

  switch (state) {
    case RUNNING_TO_SENSOR_1:
      if (sensor1Detected) {
        cycleId++;
        changeState(RUNNING_TO_CAMERA);
        sendEvent("SENSOR_DETECTED");
        Serial.println("-> Phat hien PIN! Bat dau chay den checkpoint Camera...");
      }
      break;

    case RUNNING_TO_CAMERA:
      if (elapsed >= delayToCameraMs()) {
        setMotor(PWM_DUNG);
        changeState(CAMERA_SETTLE);
      }
      break;

    case CAMERA_SETTLE:
      if (elapsed >= 400UL) {
        changeState(WAITING_FOR_CAMERA_READY);
        sendEvent("CAMERA_STOPPED");
        Serial.println("-> Dung truoc Camera. Dang cho AI xac nhan CAMERA_READY...");
      }
      break;

    case WAITING_FOR_CAMERA_READY:
      if (elapsed > 60000UL) enterFault("CAMERA_TIMEOUT");
      break;

    case WAITING_FOR_SENSOR_2:
      if (sensor2Detected) {
        changeState(RUNNING_TO_ROBOT);
        Serial.println("-> Pin da den sensor Robot. Dang chay den vi tri gap...");
      } else if (elapsed > 30000UL) {
        enterFault("ROBOT_SENSOR_TIMEOUT");
      }
      break;

    case RUNNING_TO_ROBOT:
      if (elapsed >= delayToRobotMs()) {
        setMotor(PWM_DUNG);
        changeState(WAITING_FOR_ROBOT_DONE);
        sendEvent("ROBOT_STOPPED");
        Serial.println("-> Dung tai vi tri gap. Dang cho ROBOT_DONE...");
      }
      break;

    case WAITING_FOR_ROBOT_DONE:
      if (elapsed > 120000UL) enterFault("ROBOT_TIMEOUT");
      break;

    case CLEARING_CYCLE:
      if (!sensor1Detected && !sensor2Detected) {
        if (!sensorsClearStarted) {
          sensorsClearStarted = true;
          clearStartedAt = now;
        }
        if (now - clearStartedAt >= 100UL) {
          changeState(RUNNING_TO_SENSOR_1);
          sendEvent("READY_FOR_NEXT");
        }
      } else {
        sensorsClearStarted = false;
      }
      if (elapsed > 10000UL) enterFault("SENSOR_NOT_CLEAR");
      break;

    default:
      break;
  }

  // Giu heartbeat/debug cua v2, khong dung delay.
  currentMillis = now;
  if (currentMillis - previousMillis >= 2000UL) {
    previousMillis = currentMillis;
    Serial.print("HEARTBEAT:");
    Serial.println(hostSession);
  }
}
