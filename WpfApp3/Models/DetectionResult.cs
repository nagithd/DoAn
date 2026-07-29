using System;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;

namespace WpfApp3.Models
{
    /// <summary>
    /// Represents the inspection result for a single component type
    /// </summary>
    public class ComponentInspection
    {
        public string ComponentName { get; set; } = "";
        public int Quantity { get; set; } = 0;           // Expected quantity
        public int MissQuantity { get; set; } = 0;       // Number missed
        public int BadQuantity { get; set; } = 0;        // Number found but defective
        public int GoodQuantity { get; set; } = 0;       // Number found and good
    }

    /// <summary>
    /// Represents a component detection with bounding box and confidence
    /// </summary>
    public class BoundingBox
    {
        public string ComponentName { get; set; } = "";
        public double Confidence { get; set; } = 0.0;
        public double X { get; set; } = 0.0;
        public double Y { get; set; } = 0.0;
        public double Width { get; set; } = 0.0;
        public double Height { get; set; } = 0.0;
    }

    /// <summary>
    /// Overall result status enumeration
    /// </summary>
    public enum InspectionResultStatus
    {
        MISS,   // No defects found but some expected components missing
        BAD,    // Some components are defective
        GOOD    // All components detected and good
    }

    /// <summary>
    /// Contains the complete inspection result with image, detection boxes, and statistics
    /// </summary>
    public class InspectionResult
    {
        public InspectionResultStatus Result { get; set; } = InspectionResultStatus.GOOD;
        public double Score { get; set; } = 0.0;  // Overall confidence/quality score (0-1)
        public BitmapSource? AnnotatedImage { get; set; }  // The PCB image with drawn bounding boxes
        public ObservableCollection<BoundingBox> BoundingBoxes { get; set; } = new();
        public ObservableCollection<ComponentInspection> Components { get; set; } = new();
        public DateTime InspectionTime { get; set; } = DateTime.Now;

        /// <summary>
        /// Generate sample data for testing
        /// </summary>
        public static InspectionResult CreateSampleData()
        {
            var result = new InspectionResult
            {
                Result = InspectionResultStatus.GOOD,
                Score = 0.92,  // 92% confidence
                InspectionTime = DateTime.Now
            };

            // Add sample components with inspection results
            result.Components.Add(new ComponentInspection 
            { 
                ComponentName = "Model (Q)",
                Quantity = 1,
                MissQuantity = 0,
                BadQuantity = 0,
                GoodQuantity = 1
            });
            result.Components.Add(new ComponentInspection 
            { 
                ComponentName = "IC (U)",
                Quantity = 2,
                MissQuantity = 0,
                BadQuantity = 0,
                GoodQuantity = 2
            });
            result.Components.Add(new ComponentInspection 
            { 
                ComponentName = "Resistor (R)",
                Quantity = 4,
                MissQuantity = 0,
                BadQuantity = 1,
                GoodQuantity = 3
            });
            result.Components.Add(new ComponentInspection 
            { 
                ComponentName = "Capacitor (C)",
                Quantity = 3,
                MissQuantity = 0,
                BadQuantity = 0,
                GoodQuantity = 3
            });

            // Add sample bounding boxes (detected components)
            result.BoundingBoxes.Add(new BoundingBox 
            { 
                ComponentName = "Model",
                Confidence = 0.96,
                X = 50,
                Y = 50,
                Width = 150,
                Height = 150
            });
            result.BoundingBoxes.Add(new BoundingBox 
            { 
                ComponentName = "IC",
                Confidence = 0.94,
                X = 250,
                Y = 80,
                Width = 120,
                Height = 100
            });
            result.BoundingBoxes.Add(new BoundingBox 
            { 
                ComponentName = "IC",
                Confidence = 0.91,
                X = 420,
                Y = 120,
                Width = 120,
                Height = 100
            });
            result.BoundingBoxes.Add(new BoundingBox 
            { 
                ComponentName = "Resistor",
                Confidence = 0.88,
                X = 100,
                Y = 250,
                Width = 60,
                Height = 50
            });
            result.BoundingBoxes.Add(new BoundingBox 
            { 
                ComponentName = "Resistor",
                Confidence = 0.89,
                X = 200,
                Y = 280,
                Width = 60,
                Height = 50
            });
            result.BoundingBoxes.Add(new BoundingBox 
            { 
                ComponentName = "Resistor",
                Confidence = 0.92,
                X = 310,
                Y = 250,
                Width = 60,
                Height = 50
            });
            result.BoundingBoxes.Add(new BoundingBox 
            { 
                ComponentName = "Capacitor",
                Confidence = 0.93,
                X = 150,
                Y = 360,
                Width = 70,
                Height = 60
            });
            result.BoundingBoxes.Add(new BoundingBox 
            { 
                ComponentName = "Capacitor",
                Confidence = 0.90,
                X = 280,
                Y = 340,
                Width = 70,
                Height = 60
            });
            result.BoundingBoxes.Add(new BoundingBox 
            { 
                ComponentName = "Capacitor",
                Confidence = 0.94,
                X = 390,
                Y = 360,
                Width = 70,
                Height = 60
            });

            return result;
        }
    }
}
