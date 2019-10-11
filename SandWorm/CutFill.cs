﻿using System;
using System.Collections.Generic;
using Rhino.Geometry.Intersect;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace SandWorm
{
    public class CutFill : GH_Component
    {
        public List<GeometryBase> outputSurface;
        private Curve inputRectangle;
        private double scaleFactor;
        private double sensorElevation;
        public int leftColumns = 0;
        public int rightColumns = 0;
        public int topRows = 0;
        public int bottomRows = 0;
        public int tickRate = 33; // In ms
        public SetupOptions options; // List of options coming from the SWSetup component
        public List<string> output;
        public Mesh inputMesh;
        public double[] meshElevationPoints;

        public CutFill()
          : base("CutFill", "CutFill",
              "Visualizes elevation differences between meshes.",
              "Sandworm", "Sandbox")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Rectangle", "RC", "Rectangle", GH_ParamAccess.item);
            pManager.AddMeshParameter("Mesh", "M", "Mesh to be sampled", GH_ParamAccess.item);
            pManager.AddNumberParameter("Scale Factor. 1 : ", "SF", "Scale factor for the referenced terrain.", GH_ParamAccess.item);
            pManager.AddGenericParameter("SandWormOptions", "SWO", "Setup & Calibration options", GH_ParamAccess.item);
            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGeometryParameter("Surface", "S", "Sandbox surface in real-world scale", GH_ParamAccess.list);
            pManager.AddPointParameter("Points", "P", "Additional mesh analysis", GH_ParamAccess.list);
            pManager.AddNumberParameter("Elevation Points", "E", "Resulting array of elevation points", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            options = new SetupOptions();
            
            DA.GetData<Curve>(0, ref inputRectangle);
            DA.GetData<Mesh>(1, ref inputMesh);
            DA.GetData<double>(2, ref scaleFactor);
            DA.GetData<SetupOptions>(3, ref options);

            
            if (options.sensorElevation != 0) sensorElevation = options.sensorElevation;
            if (options.leftColumns != 0) leftColumns = options.leftColumns;
            if (options.rightColumns != 0) rightColumns = options.rightColumns;
            if (options.topRows != 0) topRows = options.topRows;
            if (options.bottomRows != 0) bottomRows = options.bottomRows;
            if (options.tickRate != 0) tickRate = options.tickRate;
     

            // Shared variables
            var unitsMultiplier = Core.ConvertDrawingUnits(Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem);
            sensorElevation /= unitsMultiplier; // Standardise to mm to match sensor units
            Core.PixelSize depthPixelSize = Core.GetDepthPixelSpacing(sensorElevation);
            var trimmedWidth = (512 - leftColumns - rightColumns) * depthPixelSize.x * unitsMultiplier * scaleFactor;
            var trimmedHeight = (424 - topRows - bottomRows) * depthPixelSize.y * unitsMultiplier * scaleFactor;

            // Initialize all the outputs
            output = new List<string>();
            outputSurface = new List<GeometryBase>();
            meshElevationPoints = new double[(512 - leftColumns - rightColumns) * (424 - topRows - bottomRows)];
            var inputMeshes = new List<Mesh>();
            inputMeshes.Add(inputMesh);


            // Convert the input curve to polyline and construct a surface based on its segments
            var polyCurve = inputRectangle.ToPolyline(Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, Rhino.RhinoDoc.ActiveDoc.ModelAngleToleranceDegrees, 0.01, 100000);
            var polyLine = polyCurve.ToPolyline();
            var segments = polyLine.GetSegments();
            var plane = new Plane(segments[0].PointAt(0), segments[0].PointAt(1), segments[3].PointAt(0));
            var surface = new PlaneSurface(plane, new Interval(0, trimmedWidth), new Interval(0, trimmedHeight));

            outputSurface.Add(surface);

            // Place a point at a grid, corresponding to Kinect's depth map
            var points = new List<Point3d>();

            for (int i = 0; i < 424 - topRows - bottomRows; i++)
            {
                for (int j = 0; j < 512 - leftColumns - rightColumns; j++)
                {
                    var point = surface.PointAt(surface.Domain(0).Length / (512 - leftColumns - rightColumns) * j, surface.Domain(1).Length / (424 - topRows - bottomRows) * i);
                    points.Add(point);
                }
            }

            // Project all points onto the underlying mesh            
            var projectedPoints = Intersection.ProjectPointsToMeshes(inputMeshes, points, new Vector3d(0, 0, -1), 0.000001); // Need to use very high accuraccy, otherwise the function generates duplicate points

            // Populate the mesh elevation array
            for (int i = 0; i < meshElevationPoints.Length; i++)
            {
                meshElevationPoints[i] = (projectedPoints[i].Z / scaleFactor);
            }


            // Output data
            DA.SetDataList(0, outputSurface);
            DA.SetDataList(1, projectedPoints);
            DA.SetDataList(2, meshElevationPoints);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("daa5e88f-e0d2-47a5-928f-9f2ecbd43036"); }
        }
    }
}