using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using System.Data.Entity.Validation;
using System.Web;
using System.Web.Mvc;
using Microsoft.Ajax.Utilities;
using ProductivityAnalysisSystem.Models;
using ProductivityAnalysisSystem.Models.Interfaces;
using Type = ProductivityAnalysisSystem.Models.Type;

namespace ProductivityAnalysisSystem.Controllers
{
    public class ManageMeasurementController : Controller
    {
        private readonly IPASEntities _db = new PASEntities();
        private readonly IManageMeasurementRepository _repository;

        public ManageMeasurementController(IManageMeasurementRepository r)
        {
            _repository = r;
        }

        // GET: ManageMeasurement
        public ActionResult Index()
        {
            List<SelectListItem> inequalities = new List<SelectListItem>();
            List<SelectListItem> decimals = new List<SelectListItem>();
            List<SelectListItem> filters = new List<SelectListItem>();
            List<SelectListItem> depts = _repository.GetValidDepts();
            List<SelectListItem> filterDepts = new List<SelectListItem>();
            List<SelectListItem> ytdCalc = new List<SelectListItem>();
            
            filterDepts.Add(new SelectListItem { Text = "All Departments", Value = "0" });
            filterDepts.AddRange(depts);

            ViewBag.Depts = depts;
            ViewBag.FilterDepts = filterDepts;
            ViewBag.Category_ID = new SelectList(_db.Categories, "Category_ID", "NM");
            ViewBag.Type_ID = new SelectList(_db.Types, "Type_ID", "Type_Char");
            ViewBag.GoalCategory = new SelectList(_db.GoalCategories, "GoalCategory_ID", "NM");

            //fill ytdCalc list to be used in Add Measurement screen
            ytdCalc.Add(new SelectListItem { Text = "Sum", Value = "0" });
            ytdCalc.Add(new SelectListItem { Text = "Average", Value = "1" });
            ViewBag.YtdCalc = ytdCalc;

            //fill inequalities list to be used in Add Measurment screen
            inequalities.Add(new SelectListItem { Text = "<=", Value = "0" });
            inequalities.Add(new SelectListItem { Text = ">=", Value = "1" });
            ViewBag.Inequality = inequalities;

            //fill decimal to round to list to be used in Add Measurement screen.
            decimals.Add(new SelectListItem { Text = "0", Value = "0" });
            decimals.Add(new SelectListItem { Text = "0.0", Value = "1" });
            decimals.Add(new SelectListItem { Text = "0.00", Value = "2" });
            decimals.Add(new SelectListItem { Text = "0.000", Value = "3" });
            decimals.Add(new SelectListItem { Text = "0.0000", Value = "4" });
            decimals.Add(new SelectListItem { Text = "0.00000", Value = "5" });
            decimals.Add(new SelectListItem { Text = "0.000000", Value = "6" });
            ViewBag.Decimal = decimals;

            filters.Add(new SelectListItem { Text = "All Measurements", Value = "0" });
            filters.Add(new SelectListItem { Text = "Active", Value = "1" });
            filters.Add(new SelectListItem { Text = "Inactive", Value = "2" });
            ViewBag.Filters = filters;

            //This gets all of the measurements that are Active, Non-Calculated, and Under that particular Group/Department then places them in the view
            var measurements = _repository.GetAllAvaliableMeasurementsManageMeasurement();
            return View(measurements);
        }

        // POST: ManageMeasurements
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        public ActionResult AddMeasure()
        {
            try
            {
                Measurement measurement = new Measurement();
                measurement.Measurement_ID = measurement.Measurement_ID = (int)(DateTime.UtcNow.Ticks / 1000000);
                Goal goal = new Goal();
                CalculatedMeasurement calculatedMeasurement = new CalculatedMeasurement();

                string user = User.Identity.Name.Substring(0,20);

                //Basic Measurement Information
                string measurementName = Request["txtMeasurementName"].Trim();
                string measurementDescription = Request["txtDescription"].Trim();
                string dept = Request["Depts"];
                string typeString = Request["Type_ID"];
                string categoryString = Request["Category_ID"];
                string checkboxIsSla = Request["checkSLA"];
                string checkboxIsUnitCost = Request["checkUnitCost"];
                string decimalPlaces = Request["Decimal"];
                string ytdCalc = Request["YtdCalc"] ?? "2";

                //Goal Input Items
                string meetsValue = Request["txtMeets"].Replace(",", "").Replace("$", "").Replace("%", "");
                string meetsPlusValue = Request["txtMeetsPlus"].Replace(",", "").Replace("$", "").Replace("%", "");
                string exceedsValue = Request["txtExceeds"].Replace(",", "").Replace("$", "").Replace("%", "");
                string inequalityString = Request["Inequality"];
                string appliesAfterD = Request["dpAppliesAfter"];
                string weightString = Request["txtWeight"];
                string goalCategoryString = Request["GoalCategory"];

                //IDs from calculated measurement
                string checkboxCalculated = Request["checkCalculated"];
                string equation = Request["calculatedFormula"];

                //Department information
                if (!ValidateDeptId(dept))
                    return RedirectToAction("Index");
                measurement.Department_ID = int.Parse(dept);

                //Start Basic Information
                measurement.Activated_IN = 1;

                if (!(ValidateMeasurementName(measurementName)))
                    return RedirectToAction("Index");
                measurement.NM = measurementName;

                if (!ValidateMeasurementDescription(measurementDescription))
                    return RedirectToAction("Index");
                measurement.Description_TXT = measurementDescription;

                //set create date
                measurement.Created_DT = DateTime.Today;

                //validate measurement type
                if (!ValidateTypeId(typeString))
                    return RedirectToAction("Index");
                int type = Convert.ToInt32(typeString);
                measurement.Type_ID = type;

                //validate measure category
                if (!ValidateCategoryId(categoryString))
                    return RedirectToAction("Index");
                int category = Convert.ToInt32(categoryString);
                measurement.Category_ID = category;

                //SLA checkbox validation
                short isSla = 0;
                if (checkboxIsSla.Contains("true"))
                    isSla = 1;
                measurement.SLA_IN = isSla;

                //Unit Cost checkbox validation
                short isUnitCost = 0;
                if (checkboxIsUnitCost.Contains("true"))
                    isUnitCost = 1;
                measurement.Is_UnitCost_IN = isUnitCost;

                if (!ValidateDecimalPlaces(decimalPlaces))
                    return RedirectToAction("Index");
                measurement.Decimal_Points_SZ = Convert.ToInt32(decimalPlaces);

                if (!ValidateYtdCalc(ytdCalc))
                    return RedirectToAction("Index");
                measurement.YTD_Calc = Convert.ToInt32(ytdCalc);

                //Start of Goal infomation
                measurement.Has_Goal_IN = 0;
                if (meetsValue.Length != 0 || meetsPlusValue.Length != 0 || exceedsValue.Length != 0)
                    measurement.Has_Goal_IN = 1;

                //Goal checking. if one goal is entered, all must be entered.
                if (measurement.Has_Goal_IN != 0)
                {
                    if (!ValidateAppliesAfterDate(appliesAfterD))
                        return RedirectToAction("Index");

                    try
                    {
                        DateTime date = Convert.ToDateTime(appliesAfterD + " 12:00:00 AM");
                        goal.Applies_After_Tmstp = date;
                    }
                    catch (FormatException ex)
                    {
                        TempData["Error"] = "Date is improperly formatted! Error: " + ex.Message + "\n";
                        return RedirectToAction("Index");
                    }

                    //User identity checking
                    if (!ValidateUserName(user))
                        return RedirectToAction("Index");
                    goal.Sbmt_By = User.Identity.Name.Substring(0, 20);
                    

                    //inequality Validation
                    if (!ValidateInequalityId(inequalityString))
                        return RedirectToAction("Index");
                    int inequality = Convert.ToInt32(inequalityString[0]) - 48;

                    //Validation for goals and sets the values
                    if (!ValidateSetGoalsInequality(inequality, ref goal, meetsValue, meetsPlusValue, exceedsValue))
                        return RedirectToAction("Index");

                    //weight valiation
                    if (!ValidateWeight(weightString))
                        return RedirectToAction("Index");
                    decimal weight = Convert.ToDecimal(weightString);
                    if (weight > 0 && weight <= 1)
                        weight *= 100;
                    goal.Wgt = (int)weight;

                    //Goal Category validation
                    if (!ValidateGoalCategoryId(goalCategoryString))
                        return RedirectToAction("Index");
                    int goalCategory = Convert.ToInt32(goalCategoryString);
                    goal.GoalCategory_ID = goalCategory;

                    goal.Measurement = measurement;
                }

                //Start of Calculated Information
                //Get Calc Measurement formula and convert it to an int array
                short isCalculated = 0;
                if (checkboxCalculated.Contains("true"))
                {
                    isCalculated = 1;
                    // If valid, store
                    // (this includes the generic exception handler)
                    if (!ValidateCalculation(equation))
                        return RedirectToAction("Index");

                    calculatedMeasurement.Measurement = measurement;
                    calculatedMeasurement.Formula = equation;

                    measurement.YTD_Calc = 2;

                }
                measurement.Is_Calculated = isCalculated;

                Submit(measurement, goal, calculatedMeasurement);
                TempData["OldMeasureId"] = measurement.Measurement_ID;
            }
            catch (HttpRequestValidationException)
            {
                TempData["Error"] = "Markup is not allowed in any fields!";
            }
            catch (DbException)
            {
                TempData["Error"] = "Something went wrong in the database, please try again later!";
            }
            return RedirectToAction("Index");
        }

        //Gets the basic information for the measurement id that is passed in
        [HttpPost]
        public JsonResult GetMeasurementInfo(int measrId)
        {
            var infoDictionary = _repository.AllMeasurementInfo(measrId);
            return Json(infoDictionary, JsonRequestBehavior.DenyGet);
        }

        //method for the edit measurement modal to hit
        [HttpPost]
        public ActionResult EditMeasurement()
        {
            string measureIdString = Request["hiddenMeasureId"];
            string newMeasureName = Request["editMeasurementName"];
            string newMeasureDesc = Request["editMeasurementDescription"];
            string dept = Request["Depts"];
            string typeString = Request["Type_ID"];
            string categoryString = Request["Category_ID"];
            string newRoundingString = Request["Decimal"];
            string checkboxIsSla = Request["editCheckSLA"];
            string checkboxIsUnitCost = Request["editUnitCost"];
            string formulaString = Request["calculatedFormula"];
            string ytdTypeString = Request["YtdCalc"];

            //Validates Measure ID
            if (!ValidateMeasureId(measureIdString))
                return RedirectToAction("Index");
            int measureId = Convert.ToInt32(measureIdString);

            TempData["OldMeasureId"] = measureId;

            Measurement measure = _repository.GetMeasurementFromId(measureId);

            if (measure == null)
            {
                TempData["Error"] = "Invalid Measurement, Not Found in Database!";
                return RedirectToAction("Index");
            }

            //Name Valdiation
            if (!ValidateMeasurementName(newMeasureName))
                return RedirectToAction("Index");
            if (measure.NM != newMeasureName)
            {
                InsertIntoMeasurementAuditTable(measureId,
                    "Name changed from " + measure.NM + " to " + newMeasureName,
                    "The measurement's name was wrong.");
                measure.NM = newMeasureName;
            }

            //Description validation
            if (!ValidateMeasurementDescription(newMeasureDesc))
                return RedirectToAction("Index");
            if (measure.Description_TXT != newMeasureDesc)
            {
                InsertIntoMeasurementAuditTable(measureId,
                    "Description changed from '" + measure.Description_TXT + "' to '" + newMeasureDesc + "'",
                    "The measurement's description was wrong.");
                measure.Description_TXT = newMeasureDesc;
            }

            //Department validation
            if (!ValidateDeptId(dept))
                return RedirectToAction("Index");
            if (measure.Department_ID != int.Parse(dept))
            {
                InsertIntoMeasurementAuditTable(measureId,
                    "Department changed from " + measure.Department_ID + " to " + dept,
                    "The measurement's dept was wrong.");
                measure.Department_ID = int.Parse(dept);
            }

            //validate measurement type
            if (!ValidateTypeId(typeString))
                return RedirectToAction("Index");
            int type = Convert.ToInt32(typeString);
            if (type != measure.Type_ID)
            {
                InsertIntoMeasurementAuditTable(measureId,
                    "Type changed from " + measure.Type.Type_Char + " to " + _db.Types.Find(type).Type_Char,
                    "The measurement's type was wrong.");
                measure.Type_ID = type;
            }

            //validate measure category
            if (!ValidateCategoryId(categoryString))
                return RedirectToAction("Index");
            int category = Convert.ToInt32(categoryString);
            if (category != measure.Category_ID)
            {
                InsertIntoMeasurementAuditTable(measureId,
                    "Category changed from " + measure.Category.NM + " to " + _db.Categories.Find(category).NM,
                    "The measurement's category was wrong.");
                measure.Category_ID = category;
            }

            //Decimal validation
            if (!ValidateDecimalPlaces(newRoundingString))
                return RedirectToAction("Index");
            if (measure.Decimal_Points_SZ != decimal.Parse(newRoundingString))
            {
                InsertIntoMeasurementAuditTable(measureId,
                    "Rounding changed from " + ((int)measure.Decimal_Points_SZ) + " places to " + newRoundingString + " places",
                    "The measurement's rounding was wrong.");
                measure.Decimal_Points_SZ = Convert.ToInt32(newRoundingString);
            }

            //SLA checkbox validation
            short isSla = 0;
            if (checkboxIsSla.Contains("true"))
                isSla = 1;
            if (isSla != measure.SLA_IN)
            {
                InsertIntoMeasurementAuditTable(measureId,
                    (isSla == 1)
                        ? "SLA status changed from false to true"
                        : "SLA status changed from true to false",
                    "The measurement's SLA status was wrong.");

                measure.SLA_IN = isSla;
            }

            //Unit Cost checkbox validation
            short isUnitCost = 0;
            if (checkboxIsUnitCost.Contains("true"))
                isUnitCost = 1;
            if (isUnitCost != measure.Is_UnitCost_IN)
            {
                InsertIntoMeasurementAuditTable(measureId,
                    (isUnitCost == 1)
                        ? "Unit Cost indicator changed from false to true"
                        : "Unit Cost indicator changed from true to false",
                    "The measurement's Unit Cost status was wrong.");

                measure.Is_UnitCost_IN = isUnitCost;
            }

            //Is Calculated Validation
            if (measure.Is_Calculated == 1)
            {
                CalculatedMeasurement calculatedMeasurement = measure.CalculatedMeasurement;
                if (!ValidateCalculation(formulaString))
                    return RedirectToAction("Index");
                if (formulaString != calculatedMeasurement.Formula)
                {
                    InsertIntoMeasurementAuditTable(measureId,
                        "Formula changed from " + calculatedMeasurement.Formula + " to " + formulaString,
                        "Formula was wrong.");
                    calculatedMeasurement.Formula = formulaString;
                    _db.CalculatedMeasurements.AddOrUpdate(calculatedMeasurement);
                }
            }
            else
            {
                if (!ValidateYtdCalc(ytdTypeString))
                    return RedirectToAction("Index");
                int ytdType = int.Parse(ytdTypeString);
                if (ytdType != measure.YTD_Calc)
                {
                    InsertIntoMeasurementAuditTable(measureId,
                        "YTD calculation changed from " + 
                        ((ytdType == 0) ? "Average" : "Sum") + " to " +
                        ((ytdType == 0) ? "Sum" : "Average"),
                        "The measurement's YTD type was wrong.");
                    measure.YTD_Calc = ytdType;
                }
            }

            try
            {
                _db.Measurements.AddOrUpdate(measure);
                _db.SaveChanges();
                TempData["Success"] = "You've successfully edited " + measure.NM;
            }
            catch (DbEntityValidationException)
            {
                TempData["Error"] = "Database error!  Please try again later!";
            }
            catch (DbException)
            {
                TempData["Error"] = "Database error!  Please try again later!";
            }

            return RedirectToAction("Index");
        }

        //Method that gets call when creating a new goal
        [HttpPost]
        public ActionResult NewGoal()
        {
            Goal goal = new Goal();
            string user = User.Identity.Name;
            string measureIdString = Request["hiddenGoalMeasurementId"];
            string inequalityString = Request["Inequality"];
            string meetsString = Request["newGoalMeets"].Replace(",", "").Replace("$", "").Replace("%", "");
            string meetsPlusString = Request["newGoalMeetsPlus"].Replace(",", "").Replace("$", "").Replace("%", "");
            string exceedsString = Request["newGoalExceeds"].Replace(",", "").Replace("$", "").Replace("%", "");
            string weightString = Request["newGoalWeight"];
            string appliesAfterDateString = Request["newGoalAppliesAfter"];
            string goalCategoryString = Request["GoalCategory"];
            string goalComment = Request["newGoalComment"];
            GoalAudit goalAudit = new GoalAudit();

            //Validates Measure ID
            if (!ValidateMeasureId(measureIdString))
                return RedirectToAction("Index");
            int measureId = Convert.ToInt32(measureIdString);

            TempData["OldMeasureId"] = measureId;

            if (meetsString.Length == 0 && meetsPlusString.Length == 0 && exceedsString.Length == 0)
            {
                TempData["Error"] = "Did not save, Must have at least one goal!";
                return RedirectToAction("Index");
            }

            //User identity checking
            if (!ValidateUserName(user))
                return RedirectToAction("Index");
            goal.Sbmt_By = User.Identity.Name.Substring(0,20);

            Measurement measure = _repository.GetMeasurementFromId(measureId);

            if (measure == null)
            {
                TempData["Error"] = "Invalid Measurement, Not Found in Database!";
                return RedirectToAction("Index");
            }
            goal.Measurement_ID = measureId;

            //inequality Validation
            if (!ValidateInequalityId(inequalityString))
                return RedirectToAction("Index");
            int inequality = Convert.ToInt32(inequalityString[0]) - 48;

            //Validation for goals and sets the values
            if (!ValidateSetGoalsInequality(inequality, ref goal, meetsString, meetsPlusString, exceedsString))
                return RedirectToAction("Index");

            measure.Has_Goal_IN = 1;

            //Validates Weight
            if (!ValidateWeight(weightString))
                return RedirectToAction("Index");
            decimal weight = Convert.ToDecimal(weightString);
            if (weight > 0 && weight <= 1)
                weight *= 100;
            goal.Wgt = (int)weight;

            //Validates Applies After Date
            if (!ValidateAppliesAfterDate(appliesAfterDateString))
                return RedirectToAction("Index");
            try
            {
                DateTime date = Convert.ToDateTime(appliesAfterDateString + " 12:00:00 AM");
                goal.Applies_After_Tmstp = date;
            }
            catch (FormatException ex)
            {
                TempData["Error"] = "Date is improperly formatted! Error: " + ex.Message + "\n";
                return RedirectToAction("Index");
            }

            //Validates Goal Category
            if (!ValidateGoalCategoryId(goalCategoryString))
                return RedirectToAction("Index");
            int goalCategory = Convert.ToInt32(goalCategoryString);
            goal.GoalCategory_ID = goalCategory;

            try
            {
                _db.Measurements.AddOrUpdate(measure);
                _db.Goals.Add(goal);

                if(!ValidateComment(goalComment))
                    return RedirectToAction(("Index"));
                //Create a new entry in goalAudit table
                goalAudit.Goal = goal;
                goalAudit.Description_TXT = "A new goal was created.";
                goalAudit.Changed_DT = DateTime.Now;
                goalAudit.Reason_TXT = goalComment;
                _db.GoalAudits.Add(goalAudit);

                _db.SaveChanges();
                TempData["Success"] = "You've added a new goal to " + measure.NM + "!";

            }
            catch (DbEntityValidationException)
            {
                TempData["Error"] = "Database error, please try again later!";
            }
            catch (DbException)
            {
                TempData["Error"] = "Database error, please try again later!";
            }

            return RedirectToAction("Index");
        }

        //Method for when a measurement is deactivated or activated
        [HttpPost]
        public ActionResult DeactivateMeasurement()
        {
            //gethering strings from request object
            string measureIdString = Request["measureId"];
            string reason = Request["deactivateReason"];

            //validation section
            if (!ValidateMeasureId(measureIdString))
                return RedirectToAction("Index");
            int measureId = int.Parse(measureIdString);
            TempData["OldMeasureId"] = measureId;

            if (!ValidateReason(reason))
                return RedirectToAction("Index");

            //get old measurement, toggle the activated indicator
            var measure = _repository.GetMeasurementFromId(measureId);

            short indicator = measure.Activated_IN;
            indicator = (short)((indicator == 0)
                ? 1 
                : 0);

            measure.Activated_IN = indicator;

            MeasurementAudit measureAuditEnty = new MeasurementAudit
            {
                ChangedBy_NM = ActiveDirectoryResource.GetUsername(),
                Changed_DT = DateTime.Now,
                Measurement_ID = measureId,
                Reason_TXT = reason,
                Description_TXT = (indicator == 1)
                    ? measure.NM + " was activated"
                    : measure.NM + " was deactivated"
            };

            try
            {
                //save to db
                _db.Measurements.AddOrUpdate(measure);
                _db.MeasurementAudits.Add(measureAuditEnty);
                _db.SaveChanges();
            }
            catch (DbException)
            {
                TempData["Error"] = "Something went wrong with the database, please try again later!";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Something went wrong with the database, please try again later!";
            }

            return RedirectToAction("Index");
        }

        //Method that Get all of the audit information for the measurement
        [HttpPost]
        public JsonResult GetAuditData(string measureIdString)
        {
            Dictionary<string, List<string>> changeLog = new Dictionary<string, List<string>>();
            if (!ValidateMeasureId(measureIdString))
            {
                List<string> error = new List<string>{"Error: Measurement Id is invalid"};
                changeLog.Add(DateTime.Now.ToLongDateString() + DateTime.Now.ToLongTimeString(), error);
            }
            else
            {
                int measureId = int.Parse(measureIdString);
                changeLog = _repository.GetMeasureAuditChangeLog(measureId);
            }

            return Json(changeLog, JsonRequestBehavior.DenyGet);
        }

        //Method that saves Measurement Audit information into the audit table
        private void InsertIntoMeasurementAuditTable(int measureId, string description, string reason)
        {
            MeasurementAudit measureAuditEntry = new MeasurementAudit()
            {
                Changed_DT = DateTime.Now,
                ChangedBy_NM = ActiveDirectoryResource.GetUsername(),
                Description_TXT = description,
                Measurement_ID = measureId,
                Reason_TXT = reason
            };
            try
            {
                _db.MeasurementAudits.Add(measureAuditEntry);
            }
            catch (DbException)
            {
            }
            catch (DbUpdateException)
            {
            }
        }

        ///
        /// Validation Section
        /// 

        //Validates Department id
        public bool ValidateDeptId(string deptIdString)
        {
            if (string.IsNullOrEmpty(deptIdString))
            {
                TempData["Error"] = "Department id field cannot be empty!";
                return false;
            }
            int deptId;
            bool deptIdIsInt = int.TryParse(deptIdString, out deptId);
            if (!deptIdIsInt)
            {
                TempData["Error"] = "Department Id must be an int!";
                return false;
            }
            if (!_repository.FindItem<Department>(deptId))
            {
                TempData["Error"] = "Department Id was not in the database!";
                return false;
            }
            return true;
        }

        //Validates user name
        public bool ValidateUserName(string userName)
        {
            if (string.IsNullOrEmpty(userName))
            {
                TempData["Error"] = "Username could not be identified!<br />\n";
                return false;
            }
            if (userName.Length < 1)
            {
                TempData["Error"] = "Username is too short!!\n";
                //TODO Check on posibility of other domain names that could potentially be longer
                return false;
            }
            return true;
        }

        //validates measurement id
        public bool ValidateMeasureId(string measureIdString)
        {
            if (measureIdString.IsNullOrWhiteSpace())
            {
                TempData["Error"] = "Measure Id can't be blank!";
                return false;
            }
            int measureId;
            bool measureIdIsInt = int.TryParse(measureIdString, out measureId);
            if (!measureIdIsInt)
            {
                TempData["Error"] = "Measure Id must be an int!";
                return false;
            }

            if (!_repository.FindItem<Measurement>(measureId))
            {
                TempData["Error"] = "Measure Id doesn't exist in the db!";
                return false;
            }
            return true;
        }

        //Validate a reason that has been entered
        public bool ValidateReason(string reason)
        {
            if (reason.IsNullOrWhiteSpace())
            {
                TempData["Error"] = "Reason cannot be empty!";
                return false;
            }
            if (reason.Length > 5000)
            {
                TempData["Error"] = "Reason is too long!!\n";
                //TODO Check length of reason after we know how long it is in the database
                return false;
            }
            return true;
        }

        //If measurement name is null or greater then 256 characters, return false
        public bool ValidateMeasurementName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                TempData["Error"] = "Name cannot be blank!";
                return false;
            }
            if (name.Length <= 256) return true;

            TempData["Error"] = "Name field cannot be longer than 256 characters!";
            return false;
        }

        //If measurement description is greater that 256 characters, return false
        public bool ValidateMeasurementDescription(string description)
        {
            if (description.Length <= 256) return true;

            TempData["Error"] = "Description field cannot be longer than 256 characters!";
            return false;
        }

        //if number type is not an integer or the number type doesn't exist in the databse, return false
        public bool ValidateTypeId(string typeString)
        {
            int type;
            bool typeIsInt = int.TryParse(typeString, out type);
            if (!typeIsInt)
            {
                TempData["Error"] = "Type is not an int!!";
                return false;
            }
            if (_repository.FindItem<Type>(type)) return true;

            TempData["Error"] = "Type is invalid!!";
            return false;
        }

        //if category id is not an integer or the category doesn't exist in the databse, return false
        public bool ValidateCategoryId(string categoryString)
        {
            int category;
            bool typeIsInt = int.TryParse(categoryString, out category);
            if (!typeIsInt)
            {
                TempData["Error"] = "Category index is not an integer!!";
                return false;
            }
            if (_repository.FindItem<Category>(category)) return true;

            TempData["Error"] = "Category is invalid!!";
            return false;
        }

        //Validates Decimal Places entered
        public bool ValidateDecimalPlaces(string decimalPointsString)
        {
            int decimalPlaces;
            bool roundingIsInteger = int.TryParse(decimalPointsString, out decimalPlaces);

            if (roundingIsInteger)
            {
                if (decimalPlaces >= 0) return true;

                TempData["Error"] = "Decimal places must be positive!\n";
                return false;
            }
            TempData["Error"] = "Decimal points are incorrectly formatted!\n";
            return false;
        }

        //Validates YTD Calc entered
        public bool ValidateYtdCalc(string ytdCalcString)
        {
            int ytdCalc;
            bool roundingIsInteger = int.TryParse(ytdCalcString, out ytdCalc);

            if (roundingIsInteger)
            {
                if (ytdCalc >= 0) return true;

                TempData["Error"] = "Ytd calculation selection must be positive!\n";
                return false;
            }
            TempData["Error"] = "Ytd calculation is incorrectly formatted!\n";
            return false;
        }

        //Validates Applies After Date
        public bool ValidateAppliesAfterDate(string appliesAfterDate)
        {
            if (string.IsNullOrEmpty(appliesAfterDate))
            {
                TempData["Error"] = "Applies after date cannot be empty when there are goals entered!\n";
                return false;
            }
            DateTime appliesDate;
            bool date = DateTime.TryParse(appliesAfterDate + " 12:00:00 AM", out appliesDate);
            if (date) return true;

            TempData["Error"] = "Value entered is not an acceptable date!\n";
            return false;
        }

        //Validates the Inequality id
        public bool ValidateInequalityId(string inequalityString)
        {
            if (inequalityString.IsNullOrWhiteSpace())
            {
                TempData["Error"] = "Inequality must be filled out!";
                return false;
            }
            int inequlalityValue;
            if (!int.TryParse(inequalityString, out inequlalityValue))
            {
                TempData["Error"] = "Invalid inequality value!";
                return false;
            }
            if (inequlalityValue == 0 || inequlalityValue == 1) return true;

            TempData["Error"] = "Invalid inequality value!";
            return false;
        }

        //Validates and set goals and sets inequality
        public bool ValidateSetGoalsInequality(int inequality, ref Goal goal, string meetsValue, string meetsPlusValue, string exceedsValue)
        {
            bool error = false;
            bool meetsIsDecimal = false, meetsPlusIsDecimal = false, exceedsIsDecimal = false;

            //This allows for goals to be null. Convert to decimal only if the goal is not an empty string 
            if (meetsValue.Trim().Equals(""))
            {
                goal.Meets_Val = null;
            }
            else
            {
                decimal meetsDec;
                meetsIsDecimal = decimal.TryParse(meetsValue, out meetsDec);
                if (meetsIsDecimal)
                    goal.Meets_Val = meetsDec;
                else
                {
                    TempData["Error"] += "Meets goal must be numbers!\n";
                    error = true;
                }
            }
            if (meetsPlusValue.Trim().Equals(""))
            {
                goal.Meets_Plus_Val = null;
            }
            else
            {
                decimal meetsPlusDec;
                meetsPlusIsDecimal = decimal.TryParse(meetsPlusValue, out meetsPlusDec);
                if (meetsPlusIsDecimal)
                    goal.Meets_Plus_Val = meetsPlusDec;
                else
                {
                    TempData["Error"] += "Meets Plus goal must be numbers!\n";
                    error = true;
                }
            }

            if (exceedsValue.Trim().Equals(""))
            {
                goal.Exceeds_Val = null;
            }
            else
            {
                decimal exceedsDec;
                exceedsIsDecimal = decimal.TryParse(exceedsValue, out exceedsDec);
                if (exceedsIsDecimal)
                    goal.Exceeds_Val = exceedsDec;
                else
                {
                    TempData["Error"] += "Exceeds goal must be numbers!\n";
                    error = true;
                }
            }

            //less-than or equal to
            switch (inequality)
            {
                case 0:
                    goal.Typ = "<=";
                    if (meetsPlusIsDecimal && exceedsIsDecimal && goal.Meets_Plus_Val <= goal.Exceeds_Val)
                    {
                        TempData["Error"] += "For a <= goal, the Meets+ goal should be greater than the Exceeds goal!\n";
                        error = true;
                    }
                    if (meetsIsDecimal && meetsPlusIsDecimal && goal.Meets_Val <= goal.Meets_Plus_Val)
                    {
                        TempData["Error"] += "For a <= goal, the Meets goal should be greater than the Meets+ goal!\n";
                        error = true;
                    }
                    if (meetsIsDecimal && exceedsIsDecimal && goal.Meets_Val <= goal.Exceeds_Val)
                    {
                        TempData["Error"] += "For a <= goal, the Meets goal should be greater than the Exceeds goal!\n";
                        error = true;
                    }
                    break;
                case 1:
                    goal.Typ = ">=";
                    if (meetsIsDecimal && meetsPlusIsDecimal && goal.Meets_Val >= goal.Meets_Plus_Val)
                    {
                        TempData["Error"] += "For a >= goal, the Meets goal should be less than the Meets+ goal!\n";
                        error = true;
                    }
                    if (meetsIsDecimal && exceedsIsDecimal && goal.Meets_Val >= goal.Exceeds_Val)
                    {
                        TempData["Error"] += "For a >= goal, the Meets goal should be less than the Exceeds goal!\n";
                        error = true;
                    }
                    if (meetsPlusIsDecimal && exceedsIsDecimal && goal.Meets_Plus_Val >= goal.Exceeds_Val)
                    {
                        TempData["Error"] += "For a >= goal, the Meets+ goal should be less than the Exceeds goal!\n";
                        error = true;
                    }
                    break;
            }
            return !error;
        }

        //If weight is null, empty, not a decimal, less than zero or greater than 100, return RedirectToAction("Index");
        public bool ValidateWeight(string weightString)
        {
            if (string.IsNullOrEmpty(weightString))
            {
                TempData["Error"] = "Weight field cannot be empty if there are goals!";
                return false;
            }
            decimal weight;
            bool weightIsDecimal = decimal.TryParse(weightString, out weight);
            if (!weightIsDecimal)
            {
                TempData["Error"] = "Weight must be a number!";
                return false;
            }
            if (weight > 0 && weight <= 1)
                weight *= 100;
            if ((decimal.Compare(weight, 1) >= 0) && (decimal.Compare(weight, 100) <= 0)) return true;

            TempData["Error"] = "weight must be between 1 and 100 inclusive";
            return false;
        }

        //If goal category ID is not an integer, return false
        public bool ValidateGoalCategoryId(string goalCategoryString)
        {
            int goalCategory;
            bool typeIsInt = int.TryParse(goalCategoryString, out goalCategory);
            if (!typeIsInt)
            {
                TempData["Error"] = "Goal Category index is not an integer!!";
                return false;
            }
            if (_repository.FindItem<GoalCategory>(goalCategory)) return true;

            TempData["Error"] = "Goal Category is invalid!!";
            return false;
        }

        //This function will be used to call the expression evaluator.
        public bool ValidateCalculation(string testEquation)
        {
            if (testEquation.IsNullOrWhiteSpace())
            {
                TempData["Error"] = "The formula submitted cannot be blank!";
                return false;
            }
            // Strip out our constant indicators
            testEquation = testEquation.Replace("cn", "");
            testEquation = testEquation.Replace("cp", "");
            try
            {
                _repository.CalculateParsedString(testEquation);
            }
            // if not valid, return error and go back
            catch (InvalidOperationException)
            {
                // Not a valid equation
                TempData["Error"] = "The formula submitted has an invalid operator in it.<br />\n";
                return false;
            }
            catch (ArgumentException)
            {
                TempData["Error"] = "The formula submitted has an invalid argument.<br />\n";
                return false;
            }
            catch (DivideByZeroException)
            {
                // This should catch a Divide By Zero exception. Since these only would occur due to how
                // we're testing this formula and NOT by user error, we're going to ignore these
            }
            catch (OverflowException)
            {
                // This should catch a Overflow exception. Since these only would occur due to how
                // we're testing this formula and NOT by user error, we're going to ignore these
            }
            return true;
        }

        //Validates the comment enter by the user
        private bool ValidateComment(string goalComment)
        {
            if (goalComment.Length > 256)
            {
                TempData["Error"] = "The comment cannot be longer that 256 characters.";
                return false;
            }
            return true;
        }

        //submit to database
        private void Submit(Measurement measurement, Goal goal, CalculatedMeasurement calculatedMeasurement)
        {
            if (!ModelState.IsValid) return;

            _db.Measurements.Add(measurement);

            if (measurement.Has_Goal_IN != 0)
                _db.Goals.Add(goal);

            if (measurement.Is_Calculated == 1)
                _db.CalculatedMeasurements.Add(calculatedMeasurement);
            try
            {
                _db.SaveChanges();
                TempData["Success"] = "You have successfully added a measurement!";
            }
            catch (DbException)
            {
                TempData["Error"] = "Database error!  Try again later!";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Database error, data may have been invalid, please try again!";
            }
        }
    }
}