/*
 * Created by SharpDevelop.
 * User: Ashish
 * Date: 16-04-2020
 * Time: 04.27 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace DirectShapeExperiments
{
	[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
	[Autodesk.Revit.DB.Macros.AddInId("41D2BD96-50A4-42CE-9C79-62F50FCF2703")]
	public partial class ThisApplication
	{
		private void Module_Startup(object sender, EventArgs e)
		{

		}

		private void Module_Shutdown(object sender, EventArgs e)
		{

		}

		#region Revit Macros generated code
		private void InternalStartup()
		{
			this.Startup += new System.EventHandler(Module_Startup);
			this.Shutdown += new System.EventHandler(Module_Shutdown);
		}
		#endregion
		
		public void DrawGridInTheRoom()
		{
			Document doc = this.ActiveUIDocument.Document;
			// First get a room/ space.
			ISelectionFilter selectionFilter = new RoomSelectionFilter();
			IList<ElementId> ids =  GetUserSelectedElements("Select Room", "", selectionFilter);
			
			// Get the bounding box of that room.
			var element = doc.GetElement(ids[0]);
			var hostRoom = element as Room;
			
			getPointsGridInTheRoom(hostRoom);
		}
		
		public List<XYZ> getPointsGridInTheRoom(Room room)
		{
			Document doc = this.ActiveUIDocument.Document;
			
			BoundingBoxXYZ spaceBBox = room.get_BoundingBox(doc.ActiveView);

			XYZ bottomLeftPt = new XYZ(spaceBBox.Min.X, spaceBBox.Min.Y, 0);
			XYZ topRightPt = new XYZ(spaceBBox.Max.X, spaceBBox.Max.Y, 0);
			XYZ bottomRightPt = new XYZ(topRightPt.X, bottomLeftPt.Y, 0);
			XYZ topLeftPt = new XYZ(bottomLeftPt.X, topRightPt.Y, 0);

			Double cellSize = 3; // 100th of a foot

			// Now to construct a grid, we need to generate points.
			// Let's decide the size of the grid.
			double horizontalDist = bottomLeftPt.DistanceTo(bottomRightPt);
			double verticalDist = bottomLeftPt.DistanceTo(topLeftPt);

			int horizontalPtCount = (int) Math.Ceiling(horizontalDist / cellSize);
			int verticalPtCount = (int) Math.Ceiling(verticalDist / cellSize);
			
			// Increase 1 more to cover it fully
			horizontalPtCount++;
			verticalPtCount++;
			
			TaskDialogResult dialogResult = TaskDialog.Show("Point Count", 
			                                                String.Format("{0} points. Continue?", horizontalPtCount * verticalPtCount), 
			                                                TaskDialogCommonButtons.No | TaskDialogCommonButtons.Yes);
			
			if(dialogResult == TaskDialogResult.No)
				return null;

			List<XYZ> pointList = new List<XYZ>();


			for (int i = 0; i < verticalPtCount; i++)
			{
				for (int j = 0; j < horizontalPtCount; j++)
				{
					XYZ gridPt = new XYZ(bottomLeftPt.X + (j * cellSize), bottomLeftPt.Y + (i * cellSize), 0);
					pointList.Add(gridPt);
				}
			}
			
			// STL style index of the cells of the grid. e.g. 4,0,1,11,12 (first number is the size, next four are the indexes)
			List<int> indexList = getSTLStyleIndexList(verticalPtCount, horizontalPtCount);
			
			// Memory monitoring
			Process currentProc = Process.GetCurrentProcess();
			long memoryBeforeDrawing = currentProc.PrivateMemorySize64;
			long oneMBINBytes = 1024*1024;
			
			dialogResult = TaskDialog.Show("Point Count", String.Format(" and {0} points", pointList.Count)
			               + String.Format("\nTotal Memory consumed {0} MB", memoryBeforeDrawing / oneMBINBytes),
			              TaskDialogCommonButtons.No | TaskDialogCommonButtons.Yes);
			
			if(dialogResult == TaskDialogResult.No)
				return null;
			
			// Create a list of points which are outside the room.
			List<int> listOfPointsOutsideTheRoom = new List<int>();
			for (int i = 0; i < pointList.Count; i++) {
				bool isInsideRoom = room.IsPointInRoom(pointList[i]);
				if(isInsideRoom == false)
					listOfPointsOutsideTheRoom.Add(i);
			}
			
			// Draw for only those items which are inside the room.
			List<ElementId> dShapeElemIDs = new List<ElementId>();
			for (int i = 0; i < pointList.Count; i++) {
				if(listOfPointsOutsideTheRoom.Contains(i))
					continue;
				
				//GetTurningRadiusWithKneeAndToeClearanceDirectShape(pointList[i]);
				DirectShape dShape = GetBoxDirectShape(pointList[i], cellSize, cellSize, cellSize+1);
				dShapeElemIDs.Add(dShape.Id);
			}
			
			dialogResult = TaskDialog.Show("Collision Boxes", String.Format("There are total {0} boxes in the room", dShapeElemIDs.Count)
			                               + String.Format("\nThere are total {0} boxes outside the room", listOfPointsOutsideTheRoom.Count)
			               +  "\nContinue to remove boxes outside the room?",
			              TaskDialogCommonButtons.No | TaskDialogCommonButtons.Yes);
			if(dialogResult == TaskDialogResult.No)
				return null;
			
			if(dialogResult == TaskDialogResult.Yes)
			{
				List<ElementId> dShapeElemIDToDelete = new List<ElementId>();
				// Keep only those DirectShapes which are free from collision of other objects inside the room.
				foreach (var id in dShapeElemIDs)
	            {
	                Element element = doc.GetElement(id);
	                ICollection<ElementId> collisionElements = IdentifyCollisionObject(element, dShapeElemIDs);
	                if (collisionElements.Count > 0)
	                {
	                	dShapeElemIDToDelete.Add(id);
	                }
	            }

				// delete the colliding boxes.
				deleteElement(dShapeElemIDToDelete);
			}

			
			currentProc = Process.GetCurrentProcess();
			long memoryAfterDrawing = currentProc.PrivateMemorySize64;
			
			TaskDialog.Show("Memory Consumption", String.Format("Total Memory consumed Now {0} MB", memoryAfterDrawing / oneMBINBytes)
			               + String.Format("\nTotal Memory consumed Before drawing{0} MB", memoryBeforeDrawing / oneMBINBytes)
			               + String.Format("\nIncrease in memory consumption by {0} MB", (memoryAfterDrawing - memoryBeforeDrawing) / oneMBINBytes));
			
			return pointList;
		}
		
		private void deleteElement(ICollection<ElementId> elementIds)
        {
            try
            {
            	Document doc = this.ActiveUIDocument.Document;

                Transaction t = new Transaction(doc, "Delete Checking box");
                t.Start();
                ICollection<ElementId> deletedIdSet = doc.Delete(elementIds);
                t.Commit();
                if (0 == deletedIdSet.Count)
                {
                    throw new Exception("Deleting the selected element in Revit failed.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error Log : " + ex);
            }
        }
		
		private ICollection<ElementId> IdentifyCollisionObject(Element element, ICollection<ElementId> idsToExclude)
        {
            Document doc = this.ActiveUIDocument.Document;
            ICollection<ElementId> collisionElement = new FilteredElementCollector(doc)
                                                        .WherePasses(new ElementIntersectsElementFilter(element)).Excluding(idsToExclude).ToElementIds();
            return collisionElement;
        }
		
		private List<int> getSTLStyleIndexList(int verticalPtCount, int horizontalPtCount)
		{
			// STL style index of the cells of the grid. e.g. 4,0,1,11,12 (first number is the size, next four are the indexes)
			List<int> indexList = new List<int>();
			for (int i = 1; i < verticalPtCount; i++)
			{
				for (int j = 0; j < horizontalPtCount - 1; j++)
				{
					indexList.Add(4);
					indexList.Add(j);
					indexList.Add(j + 1);
					indexList.Add(horizontalPtCount * i + (j + 1));
					indexList.Add(horizontalPtCount * i + j);
				}
			}
			return indexList;
		}
		
		private void drawLineFromPoints(XYZ p, XYZ q, SketchPlane skPlane)
		{
			Document doc = this.ActiveUIDocument.Document;
			
			if (p.IsAlmostEqualTo(q))
            {
				//return;
                throw new System.ArgumentException("Expected two different points.");
            }
            Line line = Line.CreateBound(p, q);
            if (null == line)
            {
				//return;
                throw new Exception("Geometry line creation failed.");
            }
            
			ModelCurve mCurve = null;
            mCurve = doc.Create.NewModelCurve(line, skPlane);
		}
		
		// https://forums.autodesk.com/t5/revit-api-forum/3d-model-line/m-p/5966920/highlight/true#M13466
		//        public bool Create3DModelLine(XYZ p, XYZ q, XYZ planeOrigin)
		//        {
		//			Document doc = this.ActiveUIDocument.Document;
		//            //Document doc = DocumentInterface.getInstance().GetDoc();
		//            bool lresResult = false;
		//
		//          
		//
		//            using (Transaction tr = new Transaction(doc, "Create3DModelLine"))
		//            {
		//            	// Create a plane to draw on.
		//            	Plane plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, planeOrigin);
		//            	SketchPlane skPlane = SketchPlane.Create(doc, plane);
		//            
		//                tr.Start();
		//                try
		//                {
		//                    Line line = Line.CreateBound(p, q);
		//                    ModelCurve mCurve = null;
		//                    mCurve = doc.Create.NewModelCurve(line, skPlane);
		//                    tr.Commit();
		//                    lresResult = true;
		//                }
		//                catch (Autodesk.Revit.Exceptions.ExternalApplicationException ex)
		//                {
		//                    tr.RollBack();
		//                    throw (ex);
		//                    //MessageBox.Show(ex.Source + Environment.NewLine + ex.StackTrace + Environment.NewLine + ex.Message);
		//                }
		//            }
		//            return lresResult;
		//        }	
		
		
		public void AddQuarterCyliderAtDoor()
		{
			Document doc = this.ActiveUIDocument.Document;
			IList<ElementId> ids =  GetUserSelectedElements("Select Door", "", null);
			//TaskDialog.Show("Test", ids.Count.ToString());
			
			foreach (var elementId in ids)
			{
				
				var element = doc.GetElement(elementId);
				
				FamilyInstance doorInstance = element as FamilyInstance;
				if(doorInstance == null)
					continue;
				
				// First create a instance of the false mass family that we have imported for height checking.
				var insertionPoint = (doorInstance.Location as LocationPoint).Point;
				//doorInstance.GetSweptProfile();
				
				DirectShape drshape = GetTurningRadiusWithKneeAndToeClearanceDirectShape(insertionPoint);
				//DirectShape drshape = getDirectShapeArc(insertionPoint, 2, Math.PI, 4);
			}
			
		}
		
		public DirectShape GetBoxDirectShape(XYZ leftbottomCornerPt, double lengthAlongX, double breadthAlongY, double height)
		{			
			// First Create a profile to rotate
			// Let's take center point as the center of the turning circle.
			XYZ pt1 = new XYZ(leftbottomCornerPt.X, leftbottomCornerPt.Y, 0);
			XYZ pt2 = new XYZ(leftbottomCornerPt.X + lengthAlongX, leftbottomCornerPt.Y, 0);
			XYZ pt3 = new XYZ(leftbottomCornerPt.X + lengthAlongX, leftbottomCornerPt.Y + breadthAlongY, 0);
			XYZ pt4 = new  XYZ(leftbottomCornerPt.X, leftbottomCornerPt.Y + breadthAlongY, 0);
			
			// Document doc = DocumentInterface.getInstance().GetDoc();
			Document doc = this.ActiveUIDocument.Document;
			
			
			// Create the profile to rotate
			List<Curve> profile = new List<Curve>();
			profile.Add(Line.CreateBound(pt1, pt2));
			profile.Add(Line.CreateBound(pt2, pt3));
			profile.Add(Line.CreateBound(pt3, pt4));
			profile.Add(Line.CreateBound(pt4, pt1));

			DirectShape ds = null;
			CurveLoop curveLoop = CurveLoop.Create(profile);
			
			Solid extrudedShape = GeometryCreationUtilities.CreateExtrusionGeometry(new CurveLoop[] { curveLoop }, XYZ.BasisZ, height);
			
			using (Transaction transaction = new Transaction(doc, "Create the clearance space solid"))
			{
				transaction.Start();
				// create direct shape and assign the sphere shape
				ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));

				ds.ApplicationId = "Application id";
				ds.ApplicationDataId = "Geometry object id";
				ds.SetShape(new GeometryObject[] { extrudedShape });
				transaction.Commit();
			}
			return ds;			
		}
		
		
		public DirectShape GetTurningRadiusWithKneeAndToeClearanceDirectShape(XYZ centerPoint)
		{
			// The measurements are mentioned in the drawing in inches but we want it to be in internal units (feet)
			double inch9 = UnitUtils.ConvertToInternalUnits(9, DisplayUnitType.DUT_DECIMAL_INCHES);
			double inch13 = UnitUtils.ConvertToInternalUnits(13, DisplayUnitType.DUT_DECIMAL_INCHES);
			double inch21 = UnitUtils.ConvertToInternalUnits(21, DisplayUnitType.DUT_DECIMAL_INCHES);
			double inch24 = UnitUtils.ConvertToInternalUnits(24, DisplayUnitType.DUT_DECIMAL_INCHES);
			double inch27 = UnitUtils.ConvertToInternalUnits(27, DisplayUnitType.DUT_DECIMAL_INCHES);
			double inch30 = UnitUtils.ConvertToInternalUnits(30, DisplayUnitType.DUT_DECIMAL_INCHES);
			double inch54 = UnitUtils.ConvertToInternalUnits(54, DisplayUnitType.DUT_DECIMAL_INCHES);
			
			// First Create a profile to rotate
			// Let's take center point as the center of the turning circle.
			XYZ pt1 = new XYZ(0,0,0);
			XYZ pt2 = new XYZ(inch30,0,0);
			XYZ pt3 = new XYZ(inch30,0,inch9);
			XYZ pt4 = new XYZ(inch24,0,inch9);
			XYZ pt5 = new XYZ(inch21,0,inch27);
			XYZ pt6 = new XYZ(inch13,0,inch27);
			XYZ pt7 = new XYZ(inch13,0,inch54);
			XYZ pt8 = new XYZ(0,0,inch54);
			
			// Document doc = DocumentInterface.getInstance().GetDoc();
			Document doc = this.ActiveUIDocument.Document;
			
			
			// Create the profile to rotate
			List<Curve> profile = new List<Curve>();
			profile.Add(Line.CreateBound(pt1, pt2));
			profile.Add(Line.CreateBound(pt2, pt3));
			profile.Add(Line.CreateBound(pt3, pt4));
			profile.Add(Line.CreateBound(pt4, pt5));
			profile.Add(Line.CreateBound(pt5, pt6));
			profile.Add(Line.CreateBound(pt6, pt7));
			profile.Add(Line.CreateBound(pt7, pt8));
			profile.Add(Line.CreateBound(pt8, pt1));

			DirectShape ds = null;
			CurveLoop curveLoop = CurveLoop.Create(profile);
			
			Solid rotatedSolid = GeometryCreationUtilities.CreateRevolvedGeometry(new Frame(), new CurveLoop[] { curveLoop }, 0, Math.PI*2.0);
			
			using (Transaction transaction = new Transaction(doc, "Create the clearance space solid"))
			{
				transaction.Start();
				// create direct shape and assign the sphere shape
				ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));

				ds.ApplicationId = "Application id";
				ds.ApplicationDataId = "Geometry object id";
				ds.SetShape(new GeometryObject[] { rotatedSolid });
				
				ds.Location.Move(centerPoint); // since we have added the solid on origin, the centerPoint - origin (zero) is the direction vector.
				transaction.Commit();
			}
			return ds;
			
		}
		
		public void ArcDirectShape()
		{
			XYZ centerPoint = new XYZ(10,0,0);
			DirectShape drshape = getDirectShapeArc(centerPoint, 2, Math.PI , 4);
		}
		
		public DirectShape getDirectShapeArc(XYZ centerPoint, float radius, double angle, float height)
		{
			
			// First check whether the angle is within range.
			if(angle >=  Math.PI * 2.0)
				angle =  Math.PI * 1.99; // It will still be a solid cylinder
			
			Document doc = this.ActiveUIDocument.Document;
			//Document doc = DocumentInterface.getInstance().GetDoc();

			Plane arcPlane = Plane.CreateByNormalAndOrigin(new XYZ(0, 0, 1.0), centerPoint);
			Arc arc = Arc.Create(arcPlane, radius, 0, angle);

			Line OriginToStartPoint = Line.CreateBound(centerPoint, arc.GetEndPoint(0));
			Line OriginToEndPoint = Line.CreateBound(centerPoint, arc.GetEndPoint(1));

			// Create the profile to be extruded
			List<Curve> profile = new List<Curve>();
			profile.Add(OriginToStartPoint);
			profile.Add(arc);
			profile.Add(OriginToEndPoint.CreateReversed());

			DirectShape ds = null;
			CurveLoop curveLoop = CurveLoop.Create(profile);
			Solid arcExtrusion = GeometryCreationUtilities.CreateExtrusionGeometry(new CurveLoop[] { curveLoop }, XYZ.BasisZ, height);
			using (Transaction transaction = new Transaction(doc, "Create the DirectShapeArc"))
			{
				transaction.Start();
				// create direct shape and assign the sphere shape
				ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));

				ds.ApplicationId = "Application id";
				ds.ApplicationDataId = "Geometry object id";
				ds.SetShape(new GeometryObject[] { arcExtrusion });
				transaction.Commit();
			}
			return ds;
		}
		
		public IList<ElementId> GetUserSelectedElements(string messageBoxTitle, string messageToShow, ISelectionFilter selectionFilter)
		{
			var rvtUiDoc = this.ActiveUIDocument;
			
			IList<ElementId> elemIds = new List<ElementId>();
			try
			{
				messageToShow += "\nClick Finish button at the left top after selection is done.";
				Autodesk.Revit.UI.TaskDialog.Show(messageBoxTitle, messageToShow);
				//selection code
				Selection selection = rvtUiDoc.Selection;
				IList<Reference> benchReferences = null;
				if(selectionFilter == null)
					benchReferences = selection.PickObjects(ObjectType.Element, messageToShow);
				else
					benchReferences = selection.PickObjects(ObjectType.Element, selectionFilter, messageToShow);
					
				elemIds = (from Reference r in benchReferences select r.ElementId).ToList();
				if (0 == elemIds.Count)
				{
					TaskDialog.Show("Warning", "No Element is selected.");
				}
				//dressing.Close();
			}
			catch (Exception ex)
			{
				TaskDialog.Show("Exception", ex.Message);
				throw ex;
				//InfoLog.Message("Exception Found:" + ex);
			}
			return elemIds;
		}
	}
	
	public class RoomSelectionFilter : ISelectionFilter
	{
		public bool AllowElement(Element element)
		{
			if (element.Category.Id.IntegerValue==(int)BuiltInCategory.OST_Rooms)
			{
				return true;
			}
			return false;
		}

		public bool AllowReference(Reference refer, XYZ point)
		{
			return false;
		}
	}
}