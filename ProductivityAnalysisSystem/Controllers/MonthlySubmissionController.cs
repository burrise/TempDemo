using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web.Mvc;
using Microsoft.Ajax.Utilities;
using ProductivityAnalysisSystem.Models;
using ProductivityAnalysisSystem.Models.Interfaces;
using ProductivityAnalysisSystem.Models.ViewModels;

namespace ProductivityAnalysisSystem.Controllers
{
    public class MonthlySubmissionController : Controller
    {
        private IMonthlySubmissionRepository repository;
        private DateTime now = DateTime.Today;

        public MonthlySubmissionController(IMonthlySubmissionRepository r)
        {
            repository = r;
        }

        // GET: MonthlySubmission
        public ActionResult Index()
        {
            var applicableDate = DateTime.Today.Date;
            if (IsForLastMonth(applicableDate))
                applicableDate = applicableDate.AddMonths(-1);
            applicableDate = applicableDate.AddDays(-(DateTime.Today.Day - 1));

            var filterDepts = new List<SelectListItem>();
            filterDepts.Add(new SelectListItem { Text = "All Departments", Value = "0" });
            List<SelectListItem> depts = repository.GetValidDepts();
            filterDepts.AddRange(depts);
            ViewBag.FilterDepts = filterDepts;

            var searchSelection = new List<SelectListItem>();
            searchSelection.Add(new SelectListItem { Text = "All Measurements", Value = "0" });
            searchSelection.Add(new SelectListItem { Text = "Selected", Value = "1" });
            searchSelection.Add(new SelectListItem { Text = "Available", Value = "2" });
            ViewBag.SearchSelection = searchSelection;

            ViewBag.month = ToMonthName(applicableDate) + ' ' + applicableDate.Year;

            //This gets all of the measurements that are Active, Non-Calculated, and Under that particular Group/Department then places them in the view
            var measure = repository.GetAllActiveAvaliableMeasurementsMonthlySubmission();
            // Get session data from DB
            var measurementIds = repository.GetUserMeasurementSessionData();
            //This will store measurements that are both in the session and all active measurements. 
            List<int> measIds = new List<int>();
            foreach (var item in measure)
            {
                foreach (int t in measurementIds)
                {
                    if (t.Equals(item.MeasurementID) && item.IsActivated == 1)
                    {
                        measIds.Add(t);
                        break;
                    }
                }
            }
            Session["SubmissionIdList"] = measIds;
            if (Session["SubmissionIdList"] != null)
            {
                ViewData["ids"] = Session["SubmissionIdList"];
            }
            return View(measure);
        }

        private bool _hasCurData = false;
        private bool _hasPreData = false;
        /// <summary>
        /// Post: The data needed for the selected Measurement for viewing and entering the Monthly data
        /// </summary>
        /// <param name="mID">The ID of the Measurement in the table</param>
        /// <returns>The information that is needed to fill in the table for that particular measurement</returns>
        [HttpPost]
        public JsonResult GetMeasurementData(int mID)
        {
            Dictionary<string, object> measureInfo = new Dictionary<string, object>();

            if (ModelState.IsValid && mID > 0)
            {
                //This needs to be done becuase if the Measurement has no data point the SQL will not get data back
                var basicInfo = repository.GetInfoHasGoal(mID);

                if (basicInfo.Count > 0)
                {
                    bool hasNoData = false;
                    string ID = basicInfo[0].MeasurementID.ToString();
                    string name = basicInfo[0].NameMeasurement;
                    string _hasGoal = basicInfo[0].HasGoal.ToString();
                    var deptId = basicInfo[0].DeptId;
                    var dept = basicInfo[0].DeptName;
                    var decimalPlaces = repository.GetRoundingPlaces(basicInfo[0].MeasurementID);

                    measureInfo.Add("result", "success");
                    decimal previousValue = -100;
                    decimal currentValue = -100;
                    string goal = "";
                    string score = "";
                    bool isSev = false;
                    bool ytdIsSev = false;
                    KeyValuePair<object, object> yearToDate;
                    int dataType = 0;
                    bool hasSubmitted = false;

                    measureInfo.Add("id", ID);
                    measureInfo.Add("name", name);
                    measureInfo.Add("DeptId", deptId);
                    measureInfo.Add("DeptName", dept);

                    //Year to date items, possibly pass _hasgoal as parameter
                    yearToDate = repository.GetYearInfo(mID, ref dataType);

                    if (yearToDate.Key.Equals("Error"))
                    {
                        measureInfo.Add("yearToDate", "");
                        measureInfo.Add("yearToDateScore", "");
                    }
                    else
                    {
                        if (_hasGoal.Equals("1"))
                        {
                            string ytdGoalInformation = repository.GetGoalInfo(mID, Convert.ToDecimal(yearToDate.Value));

                            string ytdScore = repository.CalculateScore(ytdGoalInformation);
                            measureInfo.Add("yearToDateGoalInformation", ytdGoalInformation);
                            measureInfo.Add("yearToDateScore", ytdScore);
                            if (ytdGoalInformation.ToLower().Equals("sev 1") || ytdGoalInformation.ToLower().Equals("does not meet"))
                                ytdIsSev = true;
                        }
                        else
                        {
                            measureInfo.Add("yearToDateScore", "");
                        }

                        //putting yearToDate in readable form
                        var yearToDateValue = repository.GetDataPointForm(Convert.ToDecimal(yearToDate.Value), dataType, decimalPlaces);
                        measureInfo.Add("yearToDate", yearToDateValue);
                    }

                    //Current and previous month data points
                    GetDataPointValues(mID, ref previousValue, ref currentValue, ref hasNoData, ref hasSubmitted);

                    string currentMonthValue = repository.GetDataPointForm(currentValue, dataType, decimalPlaces);
                    string previousMonthValue = repository.GetDataPointForm(previousValue, dataType, decimalPlaces);

                    measureInfo.Add("hasSubmitted", hasSubmitted);

                    if (IsForLastMonth(now))
                    {
                        measureInfo.Add("currentMonthName", new DateTime(now.Year, now.Month - 1, now.Day).ToString("MMMM"));
                        measureInfo.Add("previousMonthName", new DateTime(now.Year, now.Month - 2, now.Day).ToString("MMMM"));
                    }
                    else
                    {
                        measureInfo.Add("currentMonthName", now.ToString("MMMM"));
                        measureInfo.Add("previousMonthName", new DateTime(now.Year, now.Month - 1, now.Day).ToString("MMMM"));
                    }
                    measureInfo.Add("currentMonth", hasNoData || !_hasCurData ? "" : currentMonthValue);
                    measureInfo.Add("previousMonth", hasNoData || !_hasPreData ? "" : previousMonthValue);

                    //Goal data
                    if ((_hasGoal).Equals("1") && _hasCurData)//Measurement HasGoal is true and has a current month value
                    {
                        goal = repository.GetGoalInfo(mID, currentValue);
                        score = repository.CalculateScore(goal);
                        if (goal.ToLower().Equals("sev 1") || goal.ToLower().Equals("does not meet"))
                        {
                            isSev = true;
                        }
                    }
                    measureInfo.Add("goal", goal);
                    measureInfo.Add("score", score);
                    measureInfo.Add("isSev", isSev);

                    measureInfo.Add("ytdIsSev", ytdIsSev);

                    //Add detailed goal information with < as a delimiter to be used as a tooltip

                    measureInfo.Add("allGoalInfo", GetGoalsForToolTip(mID));
                }
                else
                {
                    measureInfo.Add("result", "fail");
                    measureInfo.Add("message", "Measurment ID" + mID + "does not exist");
                }
            }
            else
            {
                measureInfo.Add("result", "fail");
                measureInfo.Add("message", "Couldn't pull measurement ID with value " + mID);
            }
            return Json(measureInfo, JsonRequestBehavior.DenyGet);
        }

        //Gets all of the reasons and comments from the database 
        [HttpPost]
        public JsonResult GetDataPointCommentReason(int mID)
        {
            List<DataPointReasonViewModel> reason = repository.GetDataPointReasons(mID);
            if (reason == null)
                TempData["Error"] = "Database error!  Please try again later";

            return Json(reason, JsonRequestBehavior.DenyGet);
        }

        /// <summary>
        /// Post: The data needed for the submitted Measurements for getting Calculated Measurements
        /// </summary>
        /// <param name="idArray">The IDs of the Measurements in the table</param>
        /// <returns>The information that is needed to fill in the table for the submitted measurement</returns>
        [HttpPost]
        public JsonResult getCalculatedMeasurementsConnectedToIds(List<string> idList)
        {
            //convert idList into integer array
            string[] idArrayStrings = idList.ToArray();
            int[] idArray = new int[idArrayStrings.Length];
            for (int i = 0; i < idArrayStrings.Length; i++)
            {
                idArray[i] = Convert.ToInt32(idArrayStrings[i]);
            }

            List<int> calcMeasureList = repository.GetCalculatedMeasurementsForIds(idArray);
            int[] calcMeasureIds = calcMeasureList.ToArray();


            return Json(calcMeasureIds, JsonRequestBehavior.DenyGet);
        }

        //Saves the reason that was entered by the user
        [HttpPost]
        public ActionResult EnterReasonGoalNotMet()
        {
            var measureIdString = Request["reasonMeasurementId"];
            var reason = Request["reasonNotMet"];
            var applicableDate = DateTime.Today.Date;

            if (IsForLastMonth(applicableDate))
                applicableDate = applicableDate.AddMonths(-1);
            applicableDate = applicableDate.AddDays(-(DateTime.Today.Day - 1));

            //Validates Measure ID
            if (!ValidateMeasureId(measureIdString))
                return RedirectToAction("Index");
            int measureId = Convert.ToInt32(measureIdString);

            if (!ValidateCommentReasonTxt(reason))
                return RedirectToAction("Index");

            int? datapointId = repository.GetDatapointId(measureId, applicableDate);
            if (!ValidateDataPoint(datapointId.ToString()))
                return RedirectToAction("Index");

            bool savedToDb = repository.SaveUpdateDataPointReason(measureId, Convert.ToInt32(datapointId), reason, true);
            if (!savedToDb)
                TempData["Error"] = "Database error!  Please try again later";
            else
                TempData["Success"] = "You have successfully saved the reason";

            return RedirectToAction("Index");
        }

        //gets all reasons an comments for the year
        [HttpPost]
        public ActionResult GetYTDReasons(int mID)
        {
            List<DataPointReasonViewModel> reason = repository.GetYTDReasons(mID);
            if (reason == null)
                TempData["Error"] = "Database error!  Please try again later";
            else
            {
                foreach (DataPointReasonViewModel item in reason)
                {
                    item.GoalInformation = repository.GetGoalInfo(item.MeasurementId, item.ValueAmt);
                }
            }
            return Json(reason, JsonRequestBehavior.DenyGet);
        }

        //saves a comment that was entered by a user
        [HttpPost]
        public ActionResult EnterCommentMessage()
        {
            var measureIdString = Request["commentMeasureId"];
            var reason = Request["commentBox"];
            var applicableDate = DateTime.Today.Date;
            if (IsForLastMonth(applicableDate))
                applicableDate = applicableDate.AddMonths(-1);
            applicableDate = applicableDate.AddDays(-(DateTime.Today.Day - 1));

            //Validates Measure ID
            if (!ValidateMeasureId(measureIdString))
                return RedirectToAction("Index");
            int measureId = Convert.ToInt32(measureIdString);

            if (!ValidateCommentReasonTxt(reason))
                return RedirectToAction("Index");

            int? datapointId = repository.GetDatapointId(measureId, applicableDate);
            if (!ValidateDataPoint(datapointId.ToString()))
            {
                TempData["Error"] = "Error! Datapoint needs to be saved before entering a comment";
                return RedirectToAction("Index");
            }

            bool savedToDb = repository.SaveUpdateDataPointReason(measureId, Convert.ToInt32(datapointId), reason, false);
            if (!savedToDb)
                TempData["Error"] = "Database error!  Please try again later";
            else
                TempData["Success"] = "You have successfully saved a comment";

            return RedirectToAction("Index");
        }

        //This is called when the save button has been hit in the view
        [HttpPost]
        public ActionResult SaveToDb()
        {
            var idReturnList = new List<int>();
            bool isChanged = false;
            var formString = Request.Params;
            var idsMapToValues = GetIdsListFromFormString(formString);
            string hasSubmitted = Request["hasSubmitted"];
            string error = "Nothing Was Changed";

            foreach (var key in idsMapToValues.Keys)
            {
                idReturnList.Add(key);
                decimal? val;
                bool valExists = idsMapToValues.TryGetValue(key, out val);
                if (valExists && val != null)
                {
                    isChanged = true;
                    var applicableDate = DateTime.Today.Date;
                    if (IsForLastMonth(applicableDate))
                        applicableDate = applicableDate.AddMonths(-1);
                    applicableDate = applicableDate.AddDays(-(DateTime.Today.Day - 1));

                    var datapoint = new Datapoint
                    {
                        Applicable_DT = applicableDate,
                        Measurement_ID = key,
                        HasSubmitted_IN = Convert.ToInt16(hasSubmitted),
                        Value_AMT = Convert.ToDecimal(val),
                        Created_DT = DateTime.Now.Date,
                        Created_TM = DateTime.Now.TimeOfDay,
                        Sbmt_By = User.Identity.Name.Substring(0,3)
                    };

                    var dId = repository.GetDatapointId(key, applicableDate);

                    //Get reason for why the datapoint did not meet a goal
                    var dataPointReason = repository.GetReasonByDataPoint(dId);
                    string goalType = repository.GetGoalInfo(datapoint.Measurement_ID, datapoint.Value_AMT);
                    if ((dataPointReason.Count == 0 && (goalType.Equals("Does Not Meet") || goalType.Equals("Sev 1"))) && hasSubmitted == "1")
                    {
                        TempData["Error"] = "All data points that don't meet goals must have a reason.";
                        return RedirectToAction("Index");
                    }

                    if (dId.HasValue)
                    {
                        datapoint.Datapoint_ID = (int)dId;
                        if (datapoint.HasSubmitted_IN == 0)
                            datapoint.HasSubmitted_IN = repository.GetSubmittedInFromId((int)dId);
                    }

                    repository.UpdateAddDataPoint(datapoint);
                }
            }

            repository.PutUserMeasurementSessionData(idReturnList);
            Session["SubmissionIdList"] = idReturnList;
            AddDatapointsToCalculatedMeasurements();

            if (isChanged)
                error = repository.SaveChanges();

            if (error.ToLower().Contains("success"))
                TempData["Success"] = "You have successfully saved data points and your selected measurements";
            else
                TempData["Error"] = error;
            if (Request["submitted"] == "true")
            {
                TempData["submitted"] = true;
            }

            return RedirectToAction("Index");
        }

        //Determines if the Saving data is for this month or last month 
        //given that they can but in data 5 business days after the month is over.
        private bool IsForLastMonth(DateTime applicableDate)
        {
            var businessDaysIntoMonth = 0;
            var day = (byte)applicableDate.Day;
            while (day > 0)
            {
                if (IsBusinessDay(applicableDate))
                    businessDaysIntoMonth++;
                day--;
                if (day > 0)
                    applicableDate = applicableDate.AddDays(-1);
            }
            return (businessDaysIntoMonth <= 5);
        }

        //This determines if the date is a business day or not
        private bool IsBusinessDay(DateTime date)
        {
            var dayOfWeek = date.DayOfWeek.ToString().ToLower();
            var isWeekend = dayOfWeek.Equals("saturday") || dayOfWeek.Equals("sunday");

            //Only holidays that have potential to be in the first five business days of the month are checked
            var isNewYears = date.Day == 1 && date.Month == 1;
            var isIndependenceDay = date.Day == 4 && date.Month == 7;
            var isLaborDay = date.Month == 9 && dayOfWeek.Equals("monday") && date.Day <= 7;

            var isHoliday = isNewYears || isIndependenceDay || isLaborDay;

            var isBusinessDay = !(isWeekend || isHoliday);

            return isBusinessDay;
        }

        //This extracts the Measurement Ids and the data for that month from the view
        public Dictionary<int, decimal?> GetIdsListFromFormString(NameValueCollection formString)
        {
            Dictionary<int, decimal?> ids = new Dictionary<int, decimal?>();

            foreach (string key in formString.AllKeys)
            {
                if (key.Contains("data_point_"))
                {
                    var val = formString.Get(key).Replace(",", "").Replace("$", "").Replace("%", "");
                    decimal valDecimal;
                    bool valDecimalIsADecimal = decimal.TryParse(val, out valDecimal);
                    int keyInt;
                    bool keyIntIsAInt = int.TryParse(key.Split('_')[2], out keyInt);
                    if (keyIntIsAInt)
                    {
                        if (valDecimalIsADecimal)
                            ids.Add(keyInt, valDecimal);
                        else
                            ids.Add(keyInt, null);
                    }
                }
            }
            return ids;
        }

        //This gets the Month in word form from the date passed in
        private static string ToMonthName(DateTime dateTime)
        {
            return dateTime.ToString("MMMM");
        }

        /// <summary>
        /// This method determines what values will be put in for the Current Month and the Previous month columns of the table
        /// </summary>
        /// <param name="mID">Measurement ID that we are looking for</param>
        /// <param name="preValue">Refrence to the prevous month value</param>
        /// <param name="curValue">Refrence to the current month value</param>
        private void GetDataPointValues(int mID, ref decimal preValue, ref decimal curValue, ref bool _hasNoData, ref bool hasSubmitted)
        {
            //The varables need to get the previous and current monthly datapoints
            bool isPrevious = IsForLastMonth(now);
            _hasNoData = false;
            DateTime start;
            DateTime end;
            if (isPrevious)
            {
                start = new DateTime(now.Year, now.AddMonths(- 2).Month, 1);
                end = new DateTime(now.Year, now.AddMonths(- 1).Month, 1); 
            }
            else
            {
                start = new DateTime(now.Year, now.AddMonths(-1).Month, 1);
                end = new DateTime(now.Year, now.Month, 1);
            }

            //SQL statement for getting all of the data needed
            var measure = repository.GetMeasurementDataPoints(mID, start, end);

            if (measure.Count == 0)
                _hasNoData = true;
            //This takes the data that was returned from the query and Adds it to a Dictionary with the information that is needed for the Monthly Submission Table
            foreach (var item in measure)
            {
                if (item.Applicable.Month == start.Month && item.Applicable.Year == start.Year)
                {
                    preValue = item.Value;
                    _hasPreData = true;
                }
                else if (item.Applicable.Month == end.Month && item.Applicable.Year == end.Year)
                {
                    hasSubmitted = Convert.ToBoolean(item.HasSubmitted);
                    curValue = item.Value;
                    _hasCurData = true;
                }
            }
        }

        //When Save button is pushed this Method will Calculate a calculated measurement if it has all of the Measurements are contained in the Calculated Measurement
        private void AddDatapointsToCalculatedMeasurements()
        {
            var calculatedMeasurements = repository.GetCalculatedMeasurements();

            var applicableDate = DateTime.Today;
            if (IsForLastMonth(applicableDate))
                applicableDate = applicableDate.AddMonths(-1);
            applicableDate = applicableDate.AddDays(-(DateTime.Today.Day - 1));

            foreach (var calculatedMeasurement in calculatedMeasurements)
            {
                var formulaString = calculatedMeasurement.Formula;
                var value = repository.CalculateMeasurement(formulaString, applicableDate);

                if (value.HasValue)
                {
                    var datapoint = new Datapoint
                    {
                        Value_AMT = (decimal)value,
                        Applicable_DT = applicableDate,
                        Created_DT = DateTime.Now,
                        Created_TM = DateTime.Now.TimeOfDay,
                        Measurement_ID = calculatedMeasurement.Measurement_ID,
                        Sbmt_By = User.Identity.Name
                    };

                    var datapointId = repository.GetDatapointId(calculatedMeasurement.Measurement_ID, applicableDate);
                    if (datapointId.HasValue)
                        datapoint.Datapoint_ID = (int)datapointId;

                    repository.UpdateAddDataPoint(datapoint);
                }
            }
            repository.SaveChanges();
        }

        //this function gets all goals for the given measurement. It will be used to provide
        //a tool tip on the monthly submission table on the goal columns
        private string GetGoalsForToolTip(int mID)
        {
            var allGoals = repository.GetGoalData(mID);
            var decimalPlaces = repository.GetRoundingPlaces(mID);

            var allGoalInfo = "";
            if (allGoals.Count > 0)
            {
                if (!allGoals[0].MeetsVal.Equals(null))
                    allGoalInfo = "Meets " + allGoals[0].Operation + " " + Math.Round((decimal)allGoals[0].MeetsVal, decimalPlaces).ToString() +
                                  Environment.NewLine;
                if (!allGoals[0].MeetsPlusVal.Equals(null))
                    allGoalInfo += "Meets+ " + allGoals[0].Operation + " " + Math.Round((decimal)allGoals[0].MeetsPlusVal, decimalPlaces) +
                                   Environment.NewLine;
                if (!allGoals[0].ExceedsVal.Equals(null))
                    allGoalInfo += "Exceeds " + allGoals[0].Operation + " " + Math.Round((decimal)allGoals[0].ExceedsVal, decimalPlaces) +
                                   Environment.NewLine;
            }
            return allGoalInfo;
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
            var measureIdIsInt = int.TryParse(measureIdString, out measureId);

            if (!measureIdIsInt)
            {
                TempData["Error"] = "Measure Id must be an int!";
                return false;
            }

            if (!repository.FindItem<Measurement>(measureId))
            {
                TempData["Error"] = "Measure Id doesn't exist in the db!";
                return false;
            }
            return true;
        }

        //Validate datapoint
        private bool ValidateDataPoint(string datapointIdString)
        {
            int datapointId;
            var datapointIdIsInt = int.TryParse(datapointIdString, out datapointId);
            if (!datapointIdIsInt)
                TempData["error"] = "Invalid datapoint id";
            else if (datapointId <= 0)
            {
                TempData["error"] = "Invalid datapoint id";
                return false;
            }
            return datapointIdIsInt;
        }

        //validate the reason why a goal was not met
        private bool ValidateCommentReasonTxt(string reason)
        {
            if (reason.Trim().Length < 1)
            {
                TempData["Error"] = "Reason must be at least one character";
                return false;
            }
            if (reason.Trim().Length > 256)
            {
                TempData["Error"] = "Reason must be 256 characters or less";
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
    }
}