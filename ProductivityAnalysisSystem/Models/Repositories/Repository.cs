using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Microsoft.Ajax.Utilities;
using System.Web.Mvc;
using System.Web.WebPages;
using System.Web.WebSockets;
using ProductivityAnalysisSystem.Models.Interfaces;
using ProductivityAnalysisSystem.Models.ViewModels;

namespace ProductivityAnalysisSystem.Models.Repositories
{
    public class Repository : IMonthlySubmissionRepository, IManageMeasurementRepository, IHistoryRepository,
        IReportRepository, IDashboardRepository
    {
        private IPASEntities db;

        public Repository(IPASEntities pas)
        {
            db = pas;
        }

        //This gets all of the measurements id and name that are Active, Non-Calculated, and Under that particular Group/Department then places them in the view
        public List<BaseFieldsViewModel> GetAllActiveAvaliableMeasurementsMonthlySubmission()
        {
            List<BaseFieldsViewModel> d = (from m in db.Measurements
                                           where m.Activated_IN == 1 && m.Is_Calculated == 0
                                           select
                                               new BaseFieldsViewModel
                                               {
                                                   MeasurementID = m.Measurement_ID,
                                                   NameMeasurement = m.NM,
                                                   DeptName = m.Department.NM,
                                                   IsActivated = m.Activated_IN
                                               }).ToList();
            return d;
        }

        //This gets all of the measurements id and name that are and Under that particular Group/Department then places them in the view
        public List<BaseFieldsViewModel> GetAllAvaliableMeasurementsManageMeasurement()
        {
            var d = (from m in db.Measurements
                     orderby m.Created_DT descending
                     select
                         new BaseFieldsViewModel
                         {
                             MeasurementID = m.Measurement_ID,
                             NameMeasurement = m.NM,
                             MeasurementType = m.Type.Type_Char,
                             MeasurementCategory = m.Category.NM,
                             HasGoal = m.Has_Goal_IN,
                             Calculated = m.Is_Calculated,
                             DeptName = m.Department.NM,
                             IsActivated = m.Activated_IN,
                             Created = m.Created_DT
                         }).ToList();
            return d;
        }

        //This Gets all of the previous and current mearsurements data point if they have any
        public List<DataPointViewModel> GetMeasurementDataPoints(int mId, DateTime start, DateTime end)
        {
            var roundingString = GetRoundingString(mId);
            List<DataPointViewModel> results = (from m in db.Measurements
                                                where m.Measurement_ID == mId
                                                join d in db.Datapoints on m.Measurement_ID equals d.Measurement_ID
                                                where d.Applicable_DT >= start && d.Applicable_DT <= end
                                                orderby d.Applicable_DT ascending
                                                select new DataPointViewModel
                                                {
                                                    MeasurementID = m.Measurement_ID,
                                                    NameMeasurement = m.NM,
                                                    YTDCalc = m.YTD_Calc,
                                                    DataPointID = d.Datapoint_ID,
                                                    Applicable = d.Applicable_DT,
                                                    Value = d.Value_AMT,
                                                    NumType = m.Type_ID,
                                                    IsCalculated = m.Is_Calculated,
                                                    HasSubmitted = d.HasSubmitted_IN,
                                                    RoudingString = roundingString
                                                }).ToList();
            return results;
        }

        //This gets the value of weather this mearsurement has a goal or not
        public List<BasicMeasurementData> GetInfoHasGoal(int mId)
        {
            List<BasicMeasurementData> info = (from m in db.Measurements
                                               where m.Measurement_ID == mId
                                               select new BasicMeasurementData
                                               {
                                                   MeasurementID = m.Measurement_ID,
                                                   NameMeasurement = m.NM,
                                                   HasGoal = m.Has_Goal_IN,
                                                   DeptId = m.Department_ID,
                                                   DeptName = m.Department.NM
                                               }).ToList();
            return info;
        }

        //This gets the goal data for a particular measurement
        public List<GoalDataViewModel> GetGoalData(int mId, DateTime? OptionalDate = null)
        {
            if (OptionalDate == null) OptionalDate = DateTime.Now;
            List<GoalDataViewModel> results = (from g in db.Goals
                                               where g.Measurement_ID == mId && g.Applies_After_Tmstp <= OptionalDate
                                               orderby g.Applies_After_Tmstp descending 
                                               select new GoalDataViewModel
                                               {
                                                   GoalID = g.Goal_ID,
                                                   MeetsVal = g.Meets_Val,
                                                   MeetsPlusVal = g.Meets_Plus_Val,
                                                   ExceedsVal = g.Exceeds_Val,
                                                   Operation = g.Typ,
                                                   AppliesAfter = g.Applies_After_Tmstp
                                               }).ToList();
            return results;
        }

        //This function gets all departments
        public List<DepartmentViewModel> GetAllDepartments()
        {
            List<DepartmentViewModel> results = (from d in db.Departments
                                                 select new DepartmentViewModel
                                                 {
                                                     DepartmentId = d.Department_ID,
                                                     DepartmentName = d.NM,
                                                     AdGroupId = d.ADGroup_ID
                                                 }).ToList();
            return results;
        }

        //Updates/Adds a datapoint passed in
        public void UpdateAddDataPoint(Datapoint datapoint)
        {
            db.Datapoints.AddOrUpdate(datapoint);
        }

        //Save Changes to db and catches errors if occured
        public string SaveChanges()
        {
            try
            {
                db.SaveChanges();
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException dbEx)
            {
                Exception raise = dbEx;
                foreach (var validationErrors in dbEx.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        string message = string.Format("{0}:{1}",
                            validationErrors.Entry.Entity.ToString(),
                            validationError.ErrorMessage);
                        // raise a new exception nesting
                        // the current instance as InnerException
                        raise = new InvalidOperationException(message, raise);
                    }
                }
                throw raise;
            }
            catch (DbException)
            {
                return "Database error!  Please try again later.";
            }
            catch (DbUpdateException)
            {
                return "Database failed to update! Please try again later!";
            }
            return "You have successfully added data points!";
        }

        //finds weather and item exist or not in the table
        public bool FindItem<T>(int idOfItem)
        {
            if (typeof(T) == typeof(Measurement))
            {
                if (db.Measurements.Find(idOfItem) != null)
                    return true;
            }
            else if (typeof(T) == typeof(Datapoint))
            {
                if (db.Datapoints.Find(idOfItem) != null)
                    return true;
            }
            else if (typeof(T) == typeof(Department))
            {
                if (db.Departments.Find(idOfItem) != null)
                    return true;
            }
            else if (typeof(T) == typeof(Type))
            {
                if (db.Types.Find(idOfItem) != null)
                    return true;
            }
            else if (typeof(T) == typeof(Category))
            {
                if (db.Categories.Find(idOfItem) != null)
                    return true;
            }
            else if (typeof(T) == typeof(GoalCategory))
            {
                if (db.GoalCategories.Find(idOfItem) != null)
                    return true;
            }
            return false;
        }

        //This gets the Datapoint ID for a particular measurement
        public int? GetDatapointId(int mId, DateTime applicableDate)
        {
            var alreadyExists =
                from n in db.Datapoints
                where n.Measurement_ID == mId && n.Applicable_DT == applicableDate
                select n.Datapoint_ID;

            foreach (var id in alreadyExists)
            {
                return id;
            }

            return null;
        }

        //get datapoint value
        public decimal? GetDataPointValue(int datapointId)
        {
            var datapointValue =
                from n in db.Datapoints
                where n.Datapoint_ID == datapointId
                select n.Value_AMT;
            foreach (var val in datapointValue)
            {
                return val;
            }
            return null;
        }

        //get all calculated measurements
        public List<CalculatedMeasurement> GetCalculatedMeasurements()
        {
            var calculatedMeasrements =
                (from n in db.CalculatedMeasurements
                 select n);

            var calculatedMeasurementList = calculatedMeasrements.ToList();

            return calculatedMeasurementList;
        }

        //creates a number of calculated datapoints for a list of months
        public List<CalculatedDataPoints> AddCalculatedDataPoints(int id, List<string> dateList)
        {
            var formula = (from n in db.CalculatedMeasurements
                           where n.Measurement_ID == id
                           select n.Formula).ToList();

            var type = (from n in db.Measurements
                        where n.Measurement_ID == id
                        select n.Type_ID).ToList();

            List<CalculatedDataPoints> calculatedDataPoints = new List<CalculatedDataPoints>();
            foreach (string dates in dateList)
            {
                CalculatedDataPoints dataItem = new CalculatedDataPoints();
                DateTime date = DateTime.Parse(dates);
                decimal? datapoint = CalculateMeasurement(formula[0], date);
                if (db.Measurements.Where(m => m.Measurement_ID == id).Select(m => m.Type.Type_Char).First().ToString().Equals("%"))
                    datapoint = datapoint * 100;
                dataItem.MeasurementID = id;
                dataItem.Formula = formula[0];
                dataItem.ApplicableDate = dates;
                dataItem.Value = datapoint;
                dataItem.Type = type[0];
                calculatedDataPoints.Add(dataItem);
            }

            return calculatedDataPoints;
        }

        //Calculates a calculated mesurements datapoint if it can
        public decimal? CalculateMeasurement(string formulaString, DateTime applicableDateTime)
        {
            var idStringArray = formulaString.Split(new[] { '+', '-', '/', '*', '(', ')' },
                StringSplitOptions.RemoveEmptyEntries);
            var operatorsArray =
                formulaString.Split(new[] { '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '.', 'c', 'p', 'n' },
                    StringSplitOptions.RemoveEmptyEntries);

            var newFormulaString = "";
            int counter = 0;

            if (formulaString.StartsWith("("))
            {
                newFormulaString += operatorsArray[0];
                counter++;
            }

            foreach (var id in idStringArray)
            {
                counter++;
                int idInt;

                var idIsInt = int.TryParse(id, out idInt);

                if (idIsInt)
                {
                    var datapointId = GetDatapointId(idInt, applicableDateTime);
                    if (datapointId.HasValue)
                    {
                        var datapointValue = GetDataPointValue((int)datapointId);
                        if (datapointValue.HasValue)
                        {
                            newFormulaString += datapointValue;
                            if (counter <= operatorsArray.Length)
                                newFormulaString += operatorsArray[counter - 1];
                        }
                        else
                            return null;
                    }
                    else
                        return null;
                }
                else
                {
                    var constant = id.Replace("cp", "");
                    constant = constant.Replace("cn", "-");

                    decimal constantInt;

                    var constantIsInt = decimal.TryParse(constant, out constantInt);
                    if (constantIsInt)
                    {
                        newFormulaString += constantInt;
                        if (counter <= operatorsArray.Length)
                            newFormulaString += operatorsArray[counter - 1];
                    }
                    else
                        return null;
                }
            }
            try
            {
                var returnValue = CalculateParsedString(newFormulaString);
                return returnValue;
            }
            catch (DivideByZeroException)
            {

            }
            catch (OverflowException)
            {

            }
            catch (ArgumentException)
            {

            }
            catch (InvalidOperationException)
            {

            }
            return null;
        }

        //this snippet of code makes a datatable.  Typically used for more complex calculations
        //we just make a table of one row with one column, put the equation in, it calculates
        //when it is made into a DataView, then we accesst the first column of the first row.
        public decimal CalculateParsedString(string newFormulaString)
        {
            var dc = new DataColumn
            {
                DataType = System.Type.GetType("System.Decimal"),
                ColumnName = "Solution",
                Expression = newFormulaString
            };
            var dt = new DataTable();
            dt.Columns.Add(dc);
            var dr = dt.NewRow();
            dt.Rows.Add(dr);
            var dv = new DataView(dt);
            var str = dv[0][0].ToString();
            return decimal.Parse(str);
        }

        //Method gets all the Creation items to a measurement so we can access a particular measurements information
        public Dictionary<string, object> AllMeasurementInfo(int mId)
        {
            Dictionary<string, object> info = new Dictionary<string, object>();
            var measureInfo = (from m in db.Measurements
                               where m.Measurement_ID == mId
                               select m).ToList();

            foreach (var item in measureInfo)
            {
                var rounding = (int)item.Decimal_Points_SZ;
                var roundingString = "";
                if (rounding > 0)
                    roundingString = ".";
                for (var i = 0; i < rounding; i++)
                    roundingString += "0";

                info.Add("Description", item.Description_TXT);
                info.Add("MeasurementType", item.Type.Type_Char);
                info.Add("MeasurementCategory", item.Category.NM);
                info.Add("DeptName", item.Department.NM);
                info.Add("HasGoal", item.Has_Goal_IN);
                info.Add("IsActivated", item.Activated_IN);
                info.Add("YtdType", GetYtdCalcName(item.YTD_Calc));
                if (item.Has_Goal_IN == 1)
                {
                    //get goal info
                    var goalInfo = (from g in db.Goals
                                    where g.Measurement_ID == mId
                                    select g).OrderByDescending(g => g.Goal_ID).Take(1);

                    foreach (var gi in goalInfo)
                    {
                        var meets = gi.Meets_Val.HasValue
                            ? ((decimal)gi.Meets_Val).ToString("###,###,###,##0" + roundingString)
                            : null;
                        var meetsp = gi.Meets_Plus_Val.HasValue
                            ? ((decimal)gi.Meets_Plus_Val).ToString("###,###,###,##0" + roundingString)
                            : null;
                        var exceeds = gi.Exceeds_Val.HasValue
                            ? ((decimal)gi.Exceeds_Val).ToString("###,###,###,##0" + roundingString)
                            : null;
                        info.Add("Meets", meets);
                        info.Add("MeetsPlus", meetsp);
                        info.Add("Exceeds", exceeds);
                        info.Add("Operator", gi.Typ);
                        info.Add("GoalWeight", gi.Wgt);
                        string date = gi.Applies_After_Tmstp.Month + "/" + gi.Applies_After_Tmstp.Day + "/" +
                                      gi.Applies_After_Tmstp.Year;
                        info.Add("AppliesAfterDate", date);
                        info.Add("GoalCategory", gi.GoalCategory.NM);
                    }
                }
                info.Add("Rounding", rounding);
                info.Add("IsCalculated", item.Is_Calculated);
                if (item.Is_Calculated == 1)
                {
                    //get formula
                    string formula = db.CalculatedMeasurements.Find(mId).Formula;
                    info.Add("IdFormula", formula);
                    string stringFormula = "";
                    var idStringArray = formula.Split(new[] { '+', '-', '/', '*', '(', ')' },
                        StringSplitOptions.RemoveEmptyEntries);
                    var operatorsArray =
                        formula.Split(new[] { '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '.', 'c', 'n', 'p' },
                            StringSplitOptions.RemoveEmptyEntries);
                    int counter = 0;

                    if (formula.StartsWith("("))
                    {
                        stringFormula += operatorsArray[0];
                        counter++;
                    }
                    foreach (var id in idStringArray)
                    {
                        int idInt;
                        var idIsInt = int.TryParse(id, out idInt);
                        counter++;
                        if (idIsInt)
                        {
                            var measurement = db.Measurements.Find(idInt);
                            if (measurement == null)
                            {
                                stringFormula = "Error Missing Measurement";
                                break;
                            }
                            var idName = measurement.NM;
                            var deptName = measurement.Department.NM;

                            stringFormula += deptName + ": " + idName;
                            if (counter <= operatorsArray.Length)
                                stringFormula += " " + operatorsArray[counter - 1] + " ";
                        }
                        else
                        {
                            var constant = id.Replace("cp", "");
                            constant = constant.Replace("cn", "-");
                            stringFormula += constant;
                            if (counter <= operatorsArray.Length)
                                stringFormula += " " + operatorsArray[counter - 1] + " ";
                        }
                    }
                    info.Add("Formula", stringFormula);
                }
                info.Add("SLA", item.SLA_IN);
                info.Add("UnitCost", item.Is_UnitCost_IN);
            }

            return info;
        }

        private string GetYtdCalcName(int ytdCalc)
        {
            switch (ytdCalc)
            {
                case 0:
                    return "Sum";
                case 1:
                    return "Average";
                case 2:
                    return "Calculated";
                default:
                    return "";
            }
        }

        //function that updates the value in datapoint at the current id
        //returns true if it succeeds, or false if it fails
        public bool UpdateDatapoint(int datapointId, decimal newValue)
        {
            Datapoint[] datapoints;

            try
            {
                datapoints = (
                    from d in db.Datapoints
                    where d.Datapoint_ID == datapointId
                    select d
                    ).ToArray();
            }
            catch (DbException)
            {
                return false;
            }

            if (datapoints.Length == 0)
                return false;

            var datapoint = datapoints[0];

            datapoint.Created_DT = DateTime.Today;
            datapoint.Created_TM = DateTime.Now.TimeOfDay;
            datapoint.Value_AMT = newValue;

            try
            {
                db.Datapoints.AddOrUpdate(datapoint);
                db.SaveChanges();
            }
            catch (DbException)
            {
                return false;
            }

            return true;
        }

        //Gets a measurement that has the measurementId
        public Measurement GetMeasurementFromId(int measurementId)
        {
            var measure = db.Measurements.Find(measurementId);
            return measure;
        }

        //Checks to see if typeId is valid
        public bool IsTypeIdValid(int typeId)
        {
            var typeIdIsValid = (db.Types.Find(typeId) != null);
            return typeIdIsValid;
        }

        public bool IsMeasurementIdValid(int measurementId)
        {
            var measureIdIsValid = (db.Measurements.Find(measurementId) != null);
            return measureIdIsValid;
        }

        //Creates a new Historical datapoint
        public string CreateNewHistoricalDataPoint(DateTime applicableDate, decimal value, int measurementId, bool OptionalIsCalculated = false)
        {
            Datapoint[] datapoints;
            try
            {
                //see if it already exist
                datapoints = (
                    from d in db.Datapoints
                    where
                        d.Applicable_DT.Month == applicableDate.Month && d.Applicable_DT.Year == applicableDate.Year &&
                        d.Measurement_ID == measurementId
                    select d
                    ).ToArray();
            }
            catch (DbException)
            {
                return "Database error!  Please try again later.";
            }
            Datapoint datapoint;
            if (datapoints.Length == 0)
            {
                datapoint = new Datapoint
                {
                    Applicable_DT = applicableDate,
                    Measurement_ID = measurementId,
                    HasSubmitted_IN = 1,
                    Value_AMT = value,
                    Created_DT = DateTime.Now.Date,
                    Created_TM = DateTime.Now.TimeOfDay,
                    Sbmt_By = ActiveDirectoryResource.GetUsername().Substring(0,3)
                };

                UpdateAddDataPoint(datapoint);
                SaveChanges();
                return "success, " + applicableDate.Month + "/" + applicableDate.Year;
            }
            if (!OptionalIsCalculated) return ", " + applicableDate.Month + "/" + applicableDate.Year;
            datapoint = datapoints.First();
            datapoint.Value_AMT = value;

            UpdateAddDataPoint(datapoint);
            SaveChanges();
            return "success, " + applicableDate.Month + "/" + applicableDate.Year;
        }

        //Method should return every dept the user is a member of
        //TODO Update method to only give valid depts once AD classes are set up, not all depts like it does now
        public List<SelectListItem> GetValidDepts()
        {
            var returnList = new List<SelectListItem>();
            var departments = new SelectList(db.Departments, "Department_ID", "NM");
            foreach (var item in departments)
            {
                returnList.Add(new SelectListItem { Text = item.Text + " -", Value = item.Value });
            }
            return returnList;
        }

        public void PutUserMeasurementSessionData(List<int> idList)
        {
            var us = new UserSession
            {
                Sso_Id = ActiveDirectoryResource.GetUsername().Substring(0,3),
                Measurement_Ids = string.Join(",", idList.ToArray())
            };

            db.UserSessions.AddOrUpdate(us);
            try
            {
                SaveChanges();
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException dbEx)
            {
                var builder = new StringBuilder("A DbUpdateException was caught while saving changes. ");

                try
                {
                    foreach (var result in dbEx.Entries)
                    {
                        builder.AppendFormat("Type: {0} was part of the problem. ", result.Entity.GetType().Name);
                    }
                }
                catch (Exception e)
                {
                    builder.Append("Error parsing DbUpdateException: " + e.ToString());
                }

                string message = builder.ToString();
                throw new Exception(message, dbEx);
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException dbEx)
            {
                Exception raise = dbEx;
                foreach (var validationErrors in dbEx.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        string message = string.Format("{0}:{1}",
                            validationErrors.Entry.Entity.ToString(),
                            validationError.ErrorMessage);
                        // raise a new exception nesting
                        // the current instance as InnerException
                        raise = new InvalidOperationException(message, raise);
                    }
                }
                throw raise;
            }
        }

        public List<int> GetUserMeasurementSessionData()
        {
            var us = db.UserSessions.Find(ActiveDirectoryResource.GetUsername().Substring(0,3));
            var idList = new List<int>();
            try
            {
                if (!us.Measurement_Ids.IsNullOrWhiteSpace())
                {
                    foreach (var item in us.Measurement_Ids.Split(','))
                    {
                        idList.Add(int.Parse(item));
                    }
                }
            }
            catch (NullReferenceException)
            {
                // If caught, leave empty list idList
            }
            return idList;
        }

        public List<int> GetCalculatedMeasurementsForIds(int[] idsArray)
        {
            var calculatedMeasurementList = GetCalculatedMeasurements();
            List<int> filteredCalculatedMeasurements = new List<int>();

            foreach (var item in calculatedMeasurementList)
            {
                var fIds = GetIdsFromFormula(item.Formula);
                var outerContinue = true;

                for (int i = 0; i < fIds.Count; i++)
                {
                    int j = Array.IndexOf(idsArray, fIds[i]);
                    if (j < 0)
                    {
                        outerContinue = false;
                        break;
                    }
                }
                if (outerContinue)
                {
                    filteredCalculatedMeasurements.Add(item.Measurement_ID);
                }
            }
            return filteredCalculatedMeasurements;
        }

        private List<int> GetIdsFromFormula(string formulaString)
        {
            var idStringArray = formulaString.Split(new[] { '+', '-', '/', '*', '(', ')' },
                StringSplitOptions.RemoveEmptyEntries);
            var formulaIdArray = new List<int>();

            foreach (var id in idStringArray)
            {
                int idInt;
                var idIsInt = int.TryParse(id, out idInt);

                if (idIsInt)
                {
                    formulaIdArray.Add(idInt);
                }
            }

            return formulaIdArray;
        }

        public short GetSubmittedInFromId(int dId)
        {
            var isSubmitted = (
                from d in db.Datapoints
                where dId == d.Datapoint_ID
                select d.HasSubmitted_IN).ToList();

            return isSubmitted.Count > 0 ? isSubmitted.First() : (short)0;
        }

        public int GetRoundingPlaces(int mId)
        {
            var decimalPlaces = (
                from m in db.Measurements
                where mId == m.Measurement_ID
                select m.Decimal_Points_SZ).ToList();

            return (int)(decimalPlaces.Count > 0 ? decimalPlaces.First() : 0);
        }

        public string GetRoundingString(int mId)
        {
            string roundingString;
            var roundingInt = GetRoundingPlaces(mId);
            if (roundingInt == 0) roundingString = "";
            else
            {
                roundingString = ".";
                for (var i = 0; i < roundingInt; i++)
                    roundingString += "0";
            }
            return roundingString;
        }

        //get list of reports
        public List<Report> GetReports()
        {
            var reports =
                (from n in db.Reports
                 select n);

            var reportsList = reports.ToList();

            return reportsList;
        }

        // Returns the title of the report passed in
        public string GetReportTitle(int reportId)
        {
            var title =
                (from n in db.Reports
                 where n.Report_ID == reportId
                 select n.NM);

            return title.First();
        }

        //Returns the type of the report passed in
        public int GetReportType(int reportId)
        {
            var type =
                (from n in db.Reports
                 where n.Report_ID == reportId
                 select n.Report_Type);

            return type.First();
        }

        public void SetReportRows(Dictionary<int, List<string>> dictRows )
        {
            foreach (var pair in dictRows)
            {
                var test = pair.Key;
                var test2 = pair.Value;
            }
        }

        public List<ReportRow> GetReportRowsForReportId(int reportId)
        {
            List<ReportRow> rows = (from n in db.ReportRows
                                    where n.Report_ID == reportId
                                    orderby n.Order
                                    select n).ToList();

            return rows;
        }
        
        // Returns the columns sorted in the correct order for a given report
        public List<ReportColumn> GetReportColumnList(int reportId)
        {
            var colList = (
                from r in db.ReportColumns
                where r.Report_ID == reportId
                orderby r.Order
                select r).ToList();

            return colList;
        }

        // Returns the columns sorted in the correct order for a given report
        public List<ReportGenerator.RowItemWithMetadata> GetReportDatapoint(int measurementId, ReportColumnsEnum columnType, DateTime startDateTime, DateTime endDateTime)
        {
            IQueryable<Datapoint> datapoints = db.Datapoints.Where(m => m.Measurement_ID == measurementId && m.Applicable_DT >= startDateTime && m.Applicable_DT <= endDateTime);
            var measure = db.Measurements.Where(m => m.Measurement_ID == measurementId).Select(m => new { m.Is_Calculated, m.Type_ID, m.Decimal_Points_SZ });
            var roundingPlaces = (int)measure.First().Decimal_Points_SZ;
            List<ReportGenerator.RowItemWithMetadata> result = new List<ReportGenerator.RowItemWithMetadata>();
            int dataType = 0;
            string yearToDate;

            switch (columnType)
            {
                case ReportColumnsEnum.CurrentMonth: // Current month
                    try
                    {
                        if (measure.First().Is_Calculated == 1)
                        {
                            List<string> date = new List<string>
                                {
                                    endDateTime.Month.ToString() + "/" + endDateTime.Year.ToString()
                                };
                            var value = AddCalculatedDataPoints(measurementId, date);
                            if (value.First().Value != null)
                                CreateNewHistoricalDataPoint(new DateTime(endDateTime.Year, endDateTime.Month, 1),
                                    Convert.ToDecimal(value.First().Value), measurementId, true);
                        }
                        var info = datapoints.Where(
                            d =>
                                d.Applicable_DT.Month == endDateTime.Month &&
                                d.Applicable_DT.Year == endDateTime.Year)
                            .Select(d => new { appliesDate = d.Applicable_DT, value = d.Value_AMT });
                        var metaData = "";
                        string monthData = GetDataPointForm(info.First().value, measure.First().Type_ID, roundingPlaces);
                        string score = GetGoalInfo(measurementId, info.First().value);
                        if (score.Equals("Sev 1"))
                        {
                            metaData = "sev1";
                        }
                        else if (score.Equals("Does Not Meet"))
                        {
                            metaData = "sev1";
                        }
                        result.Add(new ReportGenerator.RowItemWithMetadata(monthData, metaData));
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                    return result;
                case ReportColumnsEnum.PreviousMonth: // Previous month
                    try
                    {
                        if (measure.First().Is_Calculated == 1)
                        {
                            List<string> date = new List<string>
                                {
                                    (endDateTime.Month - 1).ToString() + "/" + endDateTime.Year.ToString()
                                };
                            var value = AddCalculatedDataPoints(measurementId, date);
                            if (value.First().Value != null)
                                CreateNewHistoricalDataPoint(
                                    new DateTime(endDateTime.Year, endDateTime.Month - 1, 1),
                                    Convert.ToDecimal(value.First().Value), measurementId, true);
                        }
                        var data = datapoints.Where(
                            d =>
                                d.Applicable_DT.Month == endDateTime.Month - 1 &&
                                d.Applicable_DT.Year == endDateTime.Year)
                            .Select(d => new { appliesDate = d.Applicable_DT, value = d.Value_AMT });
                        var metaData = "";
                        string monthData = GetDataPointForm(data.First().value, measure.First().Type_ID, roundingPlaces);
                        string score = GetGoalInfo(measurementId, data.First().value);
                        if (score.Equals("Sev 1"))
                        {
                            metaData = "sev1";
                        }
                        else if (score.Equals("Does Not Meet"))
                        {
                            metaData = "sev1";
                        }
                        result.Add(new ReportGenerator.RowItemWithMetadata(monthData, metaData));
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                    return result;
                case ReportColumnsEnum.PreviousMonthsInYear: // All previous months in year (multiple columns)
                    try
                    {
                        var start = new DateTime(startDateTime.Year, startDateTime.Month, 1);
                        var end = new DateTime(endDateTime.Year, endDateTime.Month, 1);
                        if (measure.First().Is_Calculated == 1)
                        {
                            List<string> dates = new List<string>();
                            for (var date = start; date <= end; date = date.AddMonths(1))
                            {
                                dates.Add(date.Month + "/" + date.Year);
                            }
                            var values = AddCalculatedDataPoints(measurementId, dates);
                            foreach (var item in values)
                            {
                                if (item.Value != null)
                                    CreateNewHistoricalDataPoint(Convert.ToDateTime(item.ApplicableDate),
                                        Convert.ToDecimal(item.Value), measurementId, true);
                            }
                        }
                        var dataInfo = datapoints.OrderBy(d => d.Applicable_DT.Month)
                                                 .ThenBy(d => d.Applicable_DT.Year)
                                .Select(d => new { appliesDate = d.Applicable_DT, value = d.Value_AMT });
                        //oldest is first should be 1/####
                        for (var date = start; date <= end; date = date.AddMonths(1))
                        {
                            var info = dataInfo.Where(d => d.appliesDate == date).ToList();
                            if (!info.Any())
                                result.Add(new ReportGenerator.RowItemWithMetadata("", ""));
                            else
                            {
                                var metaData = "";
                                string monthData = GetDataPointForm(info[0].value, measure.First().Type_ID, roundingPlaces);
                                string score = GetGoalInfo(measurementId, info.First().value);
                                if (score.Equals("Sev 1"))
                                {
                                    metaData = "sev1";
                                }
                                else if (score.Equals("Does Not Meet"))
                                {
                                    metaData = "sev1";
                                }
                                result.Add(new ReportGenerator.RowItemWithMetadata(monthData, metaData));
                            }
                        }
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                    return result;
                case ReportColumnsEnum.YTD: // YTD (given most recent year in date range)
                    try
                    {
                        //GetYearInfo returns KeyValuePair of key being either Error, Success - value string with error or YTD
                        yearToDate = GetYearToDateForYear(measurementId, ref dataType, endDateTime);
                        if (yearToDate == null)
                            result.Add(new ReportGenerator.RowItemWithMetadata("", ""));
                        else
                            result.Add(new ReportGenerator.RowItemWithMetadata(GetDataPointForm(Convert.ToDecimal(yearToDate), dataType, roundingPlaces),""));
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                    return result;
                case ReportColumnsEnum.LastYTD: // Last year
                    try
                    {
                        yearToDate = GetYearToDateForYear(measurementId, ref dataType,
                            new DateTime(endDateTime.Year - 1, 12, 1));
                        if (yearToDate == null)
                            result.Add(new ReportGenerator.RowItemWithMetadata("", ""));
                        else
                            result.Add(new ReportGenerator.RowItemWithMetadata(GetDataPointForm(Convert.ToDecimal(yearToDate), dataType, roundingPlaces), ""));
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                    return result;
                case ReportColumnsEnum.TwosAgoYTD: // 2 years ago
                    try
                    {
                        yearToDate = GetYearToDateForYear(measurementId, ref dataType,
                            new DateTime(endDateTime.Year - 2, 12, 1));
                        if (yearToDate == null)
                            result.Add(new ReportGenerator.RowItemWithMetadata("", ""));
                        else
                            result.Add(new ReportGenerator.RowItemWithMetadata(GetDataPointForm(Convert.ToDecimal(yearToDate), dataType, roundingPlaces), ""));
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                    return result;
                case ReportColumnsEnum.ThreesAgoYTD: // 3 years ago
                    try
                    {
                        yearToDate = GetYearToDateForYear(measurementId, ref dataType,
                            new DateTime(endDateTime.Year - 3, 12, 1));
                        if (yearToDate == null)
                            result.Add(new ReportGenerator.RowItemWithMetadata("", ""));
                        else
                            result.Add(new ReportGenerator.RowItemWithMetadata(GetDataPointForm(Convert.ToDecimal(yearToDate), dataType, roundingPlaces), ""));
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                    return result;
                case ReportColumnsEnum.MaxMonth: // Maximum month
                    try
                    {
                        var yearDataForMax = datapoints.Where(d => d.Applicable_DT.Year == endDateTime.Year)
                                .Select(d => new { appliesDate = d.Applicable_DT, value = d.Value_AMT }).ToList();
                        decimal? maxValue = null;
                        DateTime? maxDate = null;
                        foreach (var month in yearDataForMax)
                        {
                            if (maxValue == null || maxValue < month.value)
                            {
                                maxValue = month.value;
                                maxDate = month.appliesDate;
                            }
                        }
                        result.Add(new ReportGenerator.RowItemWithMetadata(Convert.ToDateTime(maxDate).ToString("MMMM"), ""));
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                    return result;
                case ReportColumnsEnum.MinMonth: // Minimum month
                    try
                    {
                        var yearDataForMin = datapoints.Where(d => d.Applicable_DT.Year == endDateTime.Year)
                                .Select(d => new { appliesDate = d.Applicable_DT, value = d.Value_AMT }).ToList();
                        decimal? minValue = null;
                        DateTime? minDate = null;
                        foreach (var month in yearDataForMin)
                        {
                            if (minValue == null || minValue > month.value)
                            {
                                minValue = month.value;
                                minDate = month.appliesDate;
                            }
                        }
                        result.Add(new ReportGenerator.RowItemWithMetadata(Convert.ToDateTime(minDate).ToString("MMMM"), ""));
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                    return result;
                case ReportColumnsEnum.Sum: // Sum (all values in date range)
                    try
                    {
                        var sumData =
                            datapoints.Where(d => d.Applicable_DT >= startDateTime && d.Applicable_DT <= endDateTime)
                            .Select(d => d.Value_AMT)
                            .ToList();
                        decimal? sum = null;
                        foreach (var item in sumData)
                        {
                            if (sum == null)
                                sum = item;
                            else
                                sum += item;
                        }
                        result.Add(new ReportGenerator.RowItemWithMetadata(GetDataPointForm(Convert.ToDecimal(sum), measure.First().Type_ID, roundingPlaces), ""));
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                    return result;
                case ReportColumnsEnum.Average: // Average (all values in date range)
                    try
                    {
                        var avgData =
                            datapoints.Where(d => d.Applicable_DT >= startDateTime && d.Applicable_DT <= endDateTime)
                                .Select(d => d.Value_AMT)
                                .ToList();
                        decimal? sumInfo = null;
                        foreach (var item in avgData)
                        {
                            if (sumInfo == null)
                                sumInfo = item;
                            else
                                sumInfo += item;
                        }
                        result.Add(new ReportGenerator.RowItemWithMetadata(GetDataPointForm(Convert.ToDecimal(sumInfo / avgData.Count), measure.First().Type_ID,
                                roundingPlaces), ""));
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                    return result;
                case ReportColumnsEnum.Operator: // Operator
                    try
                    {
                        var operatorString =
                                db.Goals.Where(
                                    g => g.Measurement_ID == measurementId && g.Applies_After_Tmstp <= endDateTime)
                                    .Select(g => g.Typ)
                                    .ToList();
                        result.Add(new ReportGenerator.RowItemWithMetadata(operatorString.First(), ""));
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                    return result;
                case ReportColumnsEnum.DoesNotMeet: // Does not meet
                    try
                    {
                        decimal? dnmValue = GetDoesNotMeetValue(measurementId, endDateTime);
                        if (dnmValue == null) return null;
                        result.Add(new ReportGenerator.RowItemWithMetadata(GetDataPointForm(Convert.ToDecimal(dnmValue), measure.First().Type_ID, roundingPlaces), ""));
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                    return result;
                case ReportColumnsEnum.Meets: // Meets
                    try
                    {
                        string metadata = "";
                        var goal =
                            db.Goals.Where(
                                g => g.Measurement_ID == measurementId && g.Applies_After_Tmstp <= endDateTime).OrderByDescending(g => g.Applies_After_Tmstp);
                        var goalList = goal.ToList();
                        if (goalList.Count > 1)
                        {
                            if (goalList[0].Typ.Equals("<="))
                            {
                                if (goalList[0].Meets_Val > goalList[1].Meets_Val)
                                {
                                    metadata = "LowerOrWorse";
                                }
                                if (goalList[0].Meets_Val < goalList[1].Meets_Val)
                                {
                                    metadata = "RaisedOrBetter";
                                }
                            }
                        }
                        result.Add(new ReportGenerator.RowItemWithMetadata(GetDataPointForm(Convert.ToDecimal(goal.First().Meets_Val), measure.First().Type_ID,
                                roundingPlaces), metadata));
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                    return result;
                case ReportColumnsEnum.MeetsPlus: // Meets+
                    try
                    {
                        string metadata = "";
                        var goal =
                            db.Goals.Where(
                                g => g.Measurement_ID == measurementId && g.Applies_After_Tmstp <= endDateTime).OrderByDescending(g => g.Applies_After_Tmstp);
                        var goalList = goal.ToList();
                        if (goalList.Count > 1)
                        {
                            if (goalList[0].Typ.Equals("<="))
                            {
                                if (goalList[0].Meets_Plus_Val > goalList[1].Meets_Plus_Val)
                                {
                                    metadata = "LowerOrWorse";
                                }
                                if (goalList[0].Meets_Plus_Val < goalList[1].Meets_Plus_Val)
                                {
                                    metadata = "RaisedOrBetter";
                                }
                            }
                        }
                        result.Add(new ReportGenerator.RowItemWithMetadata(GetDataPointForm(Convert.ToDecimal(goal.First().Meets_Plus_Val), measure.First().Type_ID,
                                roundingPlaces), metadata));
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                    return result;
                case ReportColumnsEnum.Exceeds: // Exceeds
                    try
                    {
                        string metadata = "";
                        var goal =
                            db.Goals.Where(
                                g => g.Measurement_ID == measurementId && g.Applies_After_Tmstp <= endDateTime).OrderByDescending(g => g.Applies_After_Tmstp);
                        var goalList = goal.ToList();
                        if (goalList.Count > 1)
                        {
                            if (goalList[0].Typ.Equals("<="))
                            {
                                if (goalList[0].Exceeds_Val > goalList[1].Exceeds_Val)
                                {
                                    metadata = "LowerOrWorse";
                                }
                                if (goalList[0].Exceeds_Val < goalList[1].Exceeds_Val)
                                {
                                    metadata = "RaisedOrBetter";
                                }
                            }
                        }
                        result.Add(new ReportGenerator.RowItemWithMetadata(GetDataPointForm(Convert.ToDecimal(goal.First().Exceeds_Val), measure.First().Type_ID,
                                roundingPlaces),metadata ));
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                    return result;
                case ReportColumnsEnum.MonthScore: // Current Month Score
                    try
                    {
                        var monthValue = datapoints.Where(
                            d =>
                                d.Applicable_DT.Month == endDateTime.Month &&
                                d.Applicable_DT.Year == endDateTime.Year)
                            .Select(d => new { appliesDate = d.Applicable_DT, value = d.Value_AMT });

                        string monthGoal = GetGoalInfo(measurementId, Convert.ToDecimal(monthValue.First().value),
                            endDateTime);
                        string monthScore = CalculateScore(monthGoal);
                        result.Add(new ReportGenerator.RowItemWithMetadata(monthScore, ""));
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                    return result;
                case ReportColumnsEnum.YTDScore: //YTD Score
                    try
                    {
                        yearToDate = GetYearToDateForYear(measurementId, ref dataType, endDateTime);
                        string ytdGoal = yearToDate == null ? GetGoalInfo(measurementId, null, endDateTime) : GetGoalInfo(measurementId, Convert.ToDecimal(yearToDate), endDateTime);
                        string ytdScore = CalculateScore(ytdGoal);
                        result.Add(new ReportGenerator.RowItemWithMetadata(ytdScore, ""));
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                    return result;
                case ReportColumnsEnum.Weight: // Weight looks at current month
                    try
                    {
                        var weightlist =
                            db.Goals.Where(
                                g => g.Measurement_ID == measurementId && g.Applies_After_Tmstp <= endDateTime)
                                    .Select(g => new { weight = g.Wgt })
                                .ToList();
                        result.Add(new ReportGenerator.RowItemWithMetadata(weightlist.First().weight + "%", ""));
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                    return result;
                default:
                    return null;
            }

        }

        //Gets the score of a goal that the number is
        public string CalculateScore(string goal)
        {
            switch (goal.ToLower())
            {
                case "exceeds":
                    return "5";
                case "meets+":
                    return "4";
                case "meets":
                    return "3";
                case "does not meet":
                    return "0";
                case "sev 1":
                    return "0";
                default:
                    return "";
            }
        }

        //Calculates the doesnotmeetvalue for a measurement if they have a goal
        public decimal? GetDoesNotMeetValue(int mID, DateTime? OptionalDate = null)
        {
            var goalInfo = GetGoalData(mID, OptionalDate);
            decimal value = 0;
            if (goalInfo.Count <= 0) return null;
            decimal? meets = goalInfo[0].MeetsVal;
            decimal? meetsPlus = goalInfo[0].MeetsPlusVal;
            decimal? exceeds = goalInfo[0].ExceedsVal;
            string operation = goalInfo[0].Operation;

            switch (operation)
            {
                case "<=":
                    if (meets != null)
                        value = (decimal)(meets + (meets * (decimal).10));
                    else if (meetsPlus != null)
                        value = (decimal)(meetsPlus + (meetsPlus * (decimal).10));
                    else if (exceeds != null)
                        value = (decimal)(exceeds + (exceeds * (decimal).10));
                    else
                        return null;
                    return value;
                case ">=":
                    if (meets != null)
                        value = (decimal)(meets - (meets * (decimal).10));
                    else if (meetsPlus != null)
                        value = (decimal)(meetsPlus - (meetsPlus * (decimal).10));
                    else if (exceeds != null)
                        value = (decimal)(exceeds - (exceeds * (decimal).10));
                    else
                        return null;
                    return value;
                default:
                    return null;
            }
        }

        //Takes vaule and determines the Goal
        public string GetGoalInfo(int mID, decimal? curValue, DateTime? OptionalDate = null)
        {
            var goalInfo = GetGoalData(mID, OptionalDate);

            if (goalInfo.Count <= 0 || curValue == -100 || curValue == null) return "";
            decimal? meets = goalInfo[0].MeetsVal;
            decimal? meetsPlus = goalInfo[0].MeetsPlusVal;
            decimal? exceeds = goalInfo[0].ExceedsVal;
            string operation = goalInfo[0].Operation;

            switch (operation)
            {
                case "<=":
                    if (curValue <= exceeds)
                        return "Exceeds";
                    if (curValue <= meetsPlus)
                        return "Meets+";
                    if (curValue <= meets)
                        return "Meets";
                    if (curValue <= meets + (meets * (decimal).10))
                        return "Does Not Meet";
                    if (meetsPlus != null)
                        if (curValue <= meetsPlus + (meetsPlus * (decimal).10))
                            return "Does Not Meet";
                    if (exceeds != null)
                        if (curValue <= exceeds + (exceeds * (decimal).10))
                            return "Does Not Meet";
                    return "Sev 1";
                case ">=":
                    if (curValue >= exceeds)
                        return "Exceeds";
                    if (curValue >= meetsPlus)
                        return "Meets+";
                    if (curValue >= meets)
                        return "Meets";
                    if (curValue >= meets - (meets * (decimal).10))
                        return "Does Not Meet";
                    if (meetsPlus != null)
                        if (curValue >= meetsPlus + (meetsPlus * (decimal).10))
                            return "Does Not Meet";
                    if (exceeds != null)
                        if (curValue >= exceeds + (exceeds * (decimal).10))
                            return "Does Not Meet";
                    return "Sev 1";
                default:
                    return "";
            }
        }

        //Calculates and formats year to date for a report
        //GetYearInfo returns KeyValuePair of key being either Error, Success - value string with error or YTD
        public string GetYearToDateForYear(int measureID, ref int dataType, DateTime dateYear)
        {
            var ytd = GetYearInfo(measureID, ref dataType, dateYear);
            if (ytd.Key.Equals("Error"))
                return null;
            return ytd.Value.ToString();
        }

        public bool CreateReport(string reportName, string reportDescription, int reportType, Dictionary<int, List<string>> dictRows, string[] arrColumns)
        {
            ReportRow reportRow = new ReportRow();
            ReportColumn reportColumn = new ReportColumn();
            try
            {
                var report = new Report
                {
                    NM = reportName,
                    Description_TXT = reportDescription,
                    Report_Type = reportType,
                };

                foreach (var row in dictRows)
                {
                    if (row.Value[0].Equals("Blank"))
                    {
                        reportRow = new ReportRow
                        {
                            Order = row.Key,
                            Row_Item = "",
                            Type = Convert.ToInt32(row.Value[1])
                        };
                    }
                    else
                    {
                        reportRow = new ReportRow
                        {
                            Order = row.Key,
                            Row_Item = row.Value[0],
                            Type = Convert.ToInt32(row.Value[1])
                        };
                    }
                    reportRow.Report = report;
                    db.ReportRows.Add(reportRow);
                }
                int index = 1;
                foreach (string col in arrColumns)
                {
                    reportColumn = new ReportColumn
                    {
                        Order = index,
                        Type = Convert.ToInt32(col)
                    };
                    reportColumn.Report = report;
                    db.ReportColumns.Add(reportColumn);
                    index++;
                }
                db.Reports.Add(report);
                SaveChanges();
            }
            catch (DbException)
            {
                return false;
            }
            return true;

        }

        public bool ValidateReportId(int reportId)
        {
            return (db.Reports.Find(reportId) != null);
        }

        public string GetRportName(int reportId)
        {
            var name = (from n in db.Reports
                        where n.Report_ID == reportId
                        select n.NM).ToList();
            return (name.Count > 0 ? name.First() : null);
        }

        public bool InsertDatapointEditAudit(string username, decimal oldValue, decimal newValue, string reason, int datapointId)
        {
            var mde = new MonthlyEntriesAudit
            {
                ChangedBy_NM = username.Substring(0,3),
                OldValue_AMT = oldValue,
                NewValue_AMT = newValue,
                Reason_TXT = reason,
                Datapoint_ID = datapointId,
                Edited_DT = DateTime.Now
            };
            try
            {
                db.MonthlyEntriesAudits.Add(mde);
                db.SaveChanges();
            }
            catch (DbException)
            {
                return false;
            }
            catch (DbUpdateException)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Gets the item to the form of what data type the item needs to be
        /// </summary>
        /// <param name="item">The item that needs to be represented in the view</param>
        /// <param name="dataType"></param>
        /// <param name="roundingPlaces">How many decimal places to show</param>
        /// <returns></returns>
        public string GetDataPointForm(decimal item, int dataType, int roundingPlaces = 2)
        {
            string itemConverted;
            string decimalPlaces;
            if (roundingPlaces > 0)
                decimalPlaces = ".";
            else
                decimalPlaces = "";

            for (var i = 0; i < roundingPlaces; i++)
                decimalPlaces += "0";

            switch (dataType)
            {
                case 1://#
                    itemConverted = item.ToString("###,###,###,##0" + decimalPlaces);
                    break;
                case 2://$
                    itemConverted = "$" + item.ToString("###,###,###,##0" + decimalPlaces);
                    break;
                case 3://%
                    itemConverted = item.ToString("###,###,###,##0" + decimalPlaces) + "%";
                    break;
                default:
                    itemConverted = "There is no data type";
                    break;
            }
            return itemConverted;
        }

        /// <summary>
        /// This Method calculates the year to day value for a basic measurement 
        /// </summary>
        /// <param name="mID">Measurement that we are looking at</param>
        /// <param name="dataType">the data type</param>
        public KeyValuePair<object, object> GetYearInfo(int mID, ref int dataType, DateTime? OptionalDate = null)
        {
            DateTime now = DateTime.Today;
            //The varables need to get the previous and current monthly datapoints
            DateTime start = new DateTime(now.Year, 1, 1);
            DateTime end = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));
            if (OptionalDate != null)
            {
                DateTime date = Convert.ToDateTime(OptionalDate);
                //The varables need to get the previous and current monthly datapoints
                start = new DateTime(date.Year, 1, 1);
                end = new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
            }
            decimal ytd = 0;
            KeyValuePair<object, object> result;
            var measure = GetMeasurementDataPoints(mID, start, end);

            if (measure.Count == 0)
            {
                result = new KeyValuePair<object, object>("Error", "Not Enough Data To Calculate");
                return result;
            }
            var yearToDateCalc = 0;
            foreach (var item in measure)
            {
                yearToDateCalc = item.YTDCalc;
                dataType = item.NumType;
                if (dataType == 3)
                    ytd += (item.Value / 100);
                else
                    ytd += item.Value;
                if (yearToDateCalc == 2)
                    break;
            }
            switch (yearToDateCalc)
            {
                case 0:
                    result = new KeyValuePair<object, object>("Success", ytd);
                    return result;
                case 1:
                    try
                    {
                        if (dataType == 3)
                            result = new KeyValuePair<object, object>("Success", (ytd / measure.Count) * 100);
                        else
                            result = new KeyValuePair<object, object>("Success", ytd / measure.Count);
                    }
                    catch (DivideByZeroException ex)
                    {
                        result = new KeyValuePair<object, object>("Error", "Can't Divide By Zero");
                        return result;
                    }
                    return result;
                case 2:
                    return GetYearToDateForCalculatedMeasurement(mID, dataType, OptionalDate);
                default:
                    result = new KeyValuePair<object, object>("Error", "Calculation was not success full do to calculation type!");
                    return result;
            }
        }

        //Gets year to date for a caluclated measurement
        public KeyValuePair<object, object> GetYearToDateForCalculatedMeasurement(int mID, int calcDataType, DateTime? OptionalDate = null)
        {
            Dictionary<int, List<KeyValuePair<object, object>>> calculatedYearToDateItems = new Dictionary<int, List<KeyValuePair<object, object>>>();
            KeyValuePair<object, object> result;
            int dataType = 0;
            decimal yearToDate;
            var formula = (from n in db.CalculatedMeasurements
                           where n.Measurement_ID == mID
                           select n.Formula).ToList();
            string formulaString = formula[0];

            List<int> ids = GetIdsFromFormula(formulaString);

            foreach (int i in ids)
            {
                List<KeyValuePair<object, object>> items = new List<KeyValuePair<object, object>>();
                items.Add(GetYearInfo(i, ref dataType, OptionalDate));
                items.Add(new KeyValuePair<object, object>("DataType", dataType));
                calculatedYearToDateItems.Add(i, items);
                if (calculatedYearToDateItems[i][0].Key.Equals("Error"))
                {
                    return calculatedYearToDateItems[i][0]; //error cant calculated
                }
            }

            var idStringArray = formulaString.Split(new[] { '+', '-', '/', '*', '(', ')' },
                StringSplitOptions.RemoveEmptyEntries);
            var operatorsArray =
                formulaString.Split(new[] { '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '.', 'c', 'p', 'n' },
                    StringSplitOptions.RemoveEmptyEntries);

            var newFormulaString = "";
            int counter = 0;

            if (formulaString.StartsWith("("))
            {
                newFormulaString += operatorsArray[0];
                counter++;
            }

            foreach (var id in idStringArray)
            {
                counter++;
                int idInt;
                var idIsInt = int.TryParse(id, out idInt);
                if (idIsInt)
                {
                    var datapointValue = calculatedYearToDateItems[idInt][0];
                    if (Convert.ToInt32(calculatedYearToDateItems[idInt][1].Value) == 3)
                        newFormulaString += Convert.ToDecimal(datapointValue.Value) / 100;
                    else
                        newFormulaString += datapointValue.Value;
                    if (counter <= operatorsArray.Length)
                        newFormulaString += operatorsArray[counter - 1];
                }
                else
                {
                    var constant = id.Replace("cp", "");
                    constant = constant.Replace("cn", "-");

                    decimal constantInt;

                    var constantIsInt = decimal.TryParse(constant, out constantInt);
                    if (constantIsInt)
                    {
                        newFormulaString += constantInt;
                        if (counter <= operatorsArray.Length)
                            newFormulaString += operatorsArray[counter - 1];
                    }
                }
            }
            try
            {
                if (calcDataType == 3)
                    yearToDate = CalculateParsedString(newFormulaString) * 100;
                else
                    yearToDate = CalculateParsedString(newFormulaString);
            }
            catch (DivideByZeroException)
            {
                result = new KeyValuePair<object, object>("Error", "Can't Divide By Zero");
                return result;
            }

            catch (InvalidOperationException)
            {
                // Not a valid equation
                result = new KeyValuePair<object, object>("Error", "The formula submitted has an invalid operator in it.<br />\n");
                return result;
            }
            catch (ArgumentException)
            {
                result = new KeyValuePair<object, object>("Error", "The formula submitted has an invalid argument.<br />\n");
                return result;
            }
            catch (OverflowException)
            {
                // This should catch a Overflow exception. Since these only would occur due to how
                // we're testing this formula and NOT by user error, we're going to ignore these
                result = new KeyValuePair<object, object>("Error", "<br />\n");
                return result;
            }

            result = new KeyValuePair<object, object>("Success", yearToDate);
            return result;
        }

        public Dictionary<string, List<string>> GetAuditChangeLog(int datapointId)
        {
            var changeLog = new Dictionary<string, List<string>>();
            var auditEntries = (from n in db.MonthlyEntriesAudits
                                where n.Datapoint_ID == datapointId
                                orderby n.Edited_DT
                                select n).ToList();

            if (auditEntries.Count == 0)
            {
                List<string> entries = new List<string>();
                entries.Add("This datapoint has never been edited");
                changeLog.Add("1", entries);
            }
            else
            {
                var roundingString = GetRoundingString(auditEntries.First().Datapoint.Measurement_ID);
                var type = auditEntries.First().Datapoint.Measurement.Type.Type_Char;
                string pre = "";
                string post = "";
                if (type == "$")
                    pre = type;
                if (type == "%")
                    post = type;

                foreach (var ae in auditEntries)
                {
                    List<string> entries = new List<string>();
                    entries.Add(ae.Edited_DT.ToString("d"));
                    entries.Add("Changed from " + pre + ae.OldValue_AMT.ToString("###,###,###,##0" + roundingString) +
                                post
                                + " to " + pre + ae.NewValue_AMT.ToString("###,###,###,##0" + roundingString) + post);
                    entries.Add(ae.ChangedBy_NM);
                    entries.Add(ae.Reason_TXT);
                    changeLog.Add(ae.Audit_ID.ToString(),entries);
                }
            }
            return changeLog;
        }

        public Dictionary<string, List<string>> GetMeasureAuditChangeLog(int measureId)
        {
            List<string> items = new List<string>();
            Dictionary<string, List<string>> changeLog = new Dictionary<string, List<string>>();

            var measurementEntries = (
                from ae in db.MeasurementAudits
                where ae.Measurement_ID == measureId
                orderby ae.Changed_DT ascending
                select ae).ToList();

            var goalAuditEntries = (
                from ga in db.GoalAudits
                where ga.Goal.Measurement_ID == measureId
                select ga).ToList();



            if (measurementEntries.Count == 0 && goalAuditEntries.Count == 0)
            {
                items.Add("This measurement hasn't been edited since it was made");
                changeLog.Add(DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString(), items);
            }
            else
            {
                foreach (var ae in measurementEntries)
                {
                    items = new List<string>();
                    items.Add(ae.Changed_DT.ToString("d"));
                    items.Add(ae.Description_TXT + ".");
                    items.Add(ae.ChangedBy_NM);
                    items.Add(ae.Reason_TXT);
                    changeLog.Add("Audit Entry " + ae.MeasurementAudit_ID, items);
                }

                foreach (var ga in goalAuditEntries)
                {
                    items = new List<string>();
                    items.Add(ga.Changed_DT.ToString("d"));
                    items.Add(ga.Description_TXT + ".");
                    items.Add(ga.Goal.Sbmt_By);
                    items.Add(ga.Reason_TXT);
                    changeLog.Add("Goal audit Entry " + ga.Goal_ID, items);
                }
            }
            changeLog = changeLog.OrderBy(d => d.Value.First()).ToDictionary(d => d.Key,
                d => d.Value);

            return changeLog;
        }



        public bool SaveUpdateDataPointReason(int measureId, int datapointId, string comment, bool isReason)
        {
            short isReasonIn = 0;
            if (isReason)
                isReasonIn = 1;

            Reason reason = new Reason
            {
                Comment_TXT = comment,
                Datapoint_ID = datapointId,
                Is_Reason_IN = isReasonIn
            };

            try
            {
                db.Reasons.AddOrUpdate(reason);
                db.SaveChanges();
            }
            catch (DbException)
            {
                return false;
            }
            catch (DbUpdateException)
            {
                return false;
            }
            return true;
        }
        //gets data point by measurementID
        public List<DataPointReasonViewModel> GetDataPointReasons(int mId)
        {
            List<DataPointReasonViewModel> reas;
            try
            {
                reas = (from d in db.Datapoints
                        where d.Measurement_ID == mId
                        join r in db.Reasons on d.Datapoint_ID equals r.Datapoint_ID
                        select new DataPointReasonViewModel
                        {
                            ReasonID = r.Reason_ID,
                            DatapointID = r.Datapoint_ID,
                            CommentTXT = r.Comment_TXT,
                            IsReasonIN = r.Is_Reason_IN
                        }).ToList();
            }
            catch (DbException)
            {
                return null;
            }
            return reas;
        }
        //gets reason by data pointID
        public List<DataPointReasonViewModel> GetReasonByDataPoint(int? datapointId)
        {
            List<DataPointReasonViewModel> reason;
            try
            {
                reason = (from r in db.Reasons
                          where r.Datapoint_ID == datapointId
                          select new DataPointReasonViewModel
                          {
                              ReasonID = r.Reason_ID,
                              DatapointID = r.Datapoint_ID,
                              CommentTXT = r.Comment_TXT,
                              IsReasonIN = r.Is_Reason_IN
                          }).ToList();
            }
            catch (DbException)
            {
                return null;
            }
            return reason;
        }

        //gets all reasons that are associated with a measurement.
        public List<DataPointReasonViewModel> GetYTDReasons(int mId)
        {
            List<DataPointReasonViewModel> reason;
            try
            {
                reason = (from m in db.Measurements
                          where m.Measurement_ID == mId
                          join d in db.Datapoints on m.Measurement_ID equals d.Measurement_ID
                          join r in db.Reasons on d.Datapoint_ID equals r.Datapoint_ID
                          orderby d.Created_DT descending
                          select new DataPointReasonViewModel
                          {
                              ReasonID = r.Reason_ID,
                              DatapointID = r.Datapoint_ID,
                              CommentTXT = r.Comment_TXT,
                              IsReasonIN = r.Is_Reason_IN,
                              MeasurementId = m.Measurement_ID,
                              SubmittedBy = d.Sbmt_By,
                              Created = d.Created_DT.Month + "/" + d.Created_DT.Year,
                              ValueAmt = d.Value_AMT
                          }).ToList();
            }
            catch (DbException)
            {
                return null;
            }
            return reason;
        }

        public bool DoesGroupExist(string adGroupId)
        {
            var exists = (from d in db.Departments
                          where d.ADGroup_ID == adGroupId
                          select d
                ).ToList().Count > 0;
            return exists;
        }

        public void InsertGroup(string adGroupId, string groupName)
        {
            var newDepartment = new Department()
            {
                ADGroup_ID = adGroupId,
                NM = groupName
            };
            db.Departments.Add(newDepartment);
            db.SaveChanges();
        }

        public void DeleteReport(int reportId)
        {
            try
            {
                db.Reports.Remove(db.Reports.Find(reportId));
                db.SaveChanges();
            }
            
            catch (DbException)
            {

            }
        } 
    }
}
