using System;
using System.Collections.Generic;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using Microsoft.Ajax.Utilities;
using Newtonsoft.Json;
using ProductivityAnalysisSystem.Models.Interfaces;
using ProductivityAnalysisSystem.Models.ViewModels;

namespace ProductivityAnalysisSystem.Controllers
{
    public class HistoryController : Controller
    {
        private readonly IHistoryRepository _repository;

        public HistoryController(IHistoryRepository r)
        {
            _repository = r;
        }

        // GET: History
        public ActionResult Index()
        {
            var filterDepts = new List<SelectListItem>();
            var filters = new List<SelectListItem>();

            filterDepts.Add(new SelectListItem { Text = "All Departments", Value = "0" });
            List<SelectListItem> depts = _repository.GetValidDepts();
            filterDepts.AddRange(depts);

            filters.Add(new SelectListItem { Text = "All Measurements", Value = "0" });
            filters.Add(new SelectListItem { Text = "Active Measurements", Value = "1" });
            filters.Add(new SelectListItem { Text = "Inactive Measurements", Value = "2" });

            ViewBag.Filters = filters;
            ViewBag.FilterDepts = filterDepts;

            var measurements = _repository.GetAllAvaliableMeasurementsManageMeasurement();
            return View(measurements);
        }

        //Gets audit information associated with a datapoint.
        [HttpPost]
        public JsonResult GetAuditData(string datapointString)
        {
            Dictionary<string, List<string>> changeLog;
            if (!ValidateDataPoint(datapointString))
            {
                changeLog = new Dictionary<string, List<string>>();
                List<string> entry = new List<string>();
                entry.Add("Error: Datapoint is invalid");
                changeLog.Add("-1", entry);//check for -1 and display appropriate error message
            }
            else
            {
                var datapointId = int.Parse(datapointString);
                changeLog = _repository.GetAuditChangeLog(datapointId);
            }

            return Json(changeLog, JsonRequestBehavior.DenyGet);
        }

        //Method that gets all the data points that exist for a particular measurement
        [HttpPost]
        public JsonResult GetAllDataPoints(int measrId)
        {
            List<Dictionary<string, object>> datapoints = new List<Dictionary<string, object>>();
            var measurementDataPoints = _repository.GetMeasurementDataPoints(measrId, DateTime.MinValue, DateTime.MaxValue);

            foreach (var dp in measurementDataPoints)
            {
                var datapointInfo = new Dictionary<string, object>();
                string date = dp.Applicable.Month + "/" + dp.Applicable.Year;
                datapointInfo.Add("DataPointID", dp.DataPointID);
                datapointInfo.Add("Date", date);
                datapointInfo.Add("DataPoint", GetDataPointForm(dp.Value, dp.NumType, dp.RoudingString));
                datapoints.Add(datapointInfo);
            }
            return Json(datapoints, JsonRequestBehavior.DenyGet);
        }

        //Gets all date between two dates specified
        [HttpPost]
        public JsonResult GetDates(string fromDate, string toDate)
        {
            List<string> dates = new List<string>();
            DateTime now = DateTime.Today;
            if (ValidateDateRange(fromDate, toDate))
            {
                var start = DateTime.Parse(fromDate);
                start = new DateTime(start.Year, start.Month, 1);
                var end = DateTime.Parse(toDate);
                end = new DateTime(end.Year, end.Month, 1);
                for (var date = start; date <= end; date = date.AddMonths(1))
                {
                    if (date < (new DateTime(now.Year, now.Month - 1, now.Day)))
                        dates.Add(date.Month + "/" + date.Year);
                }
            }
            return Json(dates, JsonRequestBehavior.DenyGet);
        }

        //Get and Calculate Datapoints between two dates
        [HttpPost]
        public JsonResult GetCalculatedDatapoints(string fromDate, string toDate, string mIdString)
        {
            List<Dictionary<string, object>> datapointList = new List<Dictionary<string, object>>();
            List<string> dateList = JsonConvert.DeserializeObject<List<string>>(new JavaScriptSerializer().Serialize(GetDates(fromDate, toDate).Data));
            List<string> error = new List<string> { "Data Point for " };
            List<string> success = new List<string> { "You Have Added/Updated Historical Data Points for: " };

            if (ValidateMeasurement(mIdString))
            {
                int mid = Convert.ToInt32(mIdString);
                List<CalculatedDataPoints> calculatedData = _repository.AddCalculatedDataPoints(mid, dateList);
                foreach (var data in calculatedData)
                {
                    var calculatedDataPoints = new Dictionary<string, object> { { "Date", data.ApplicableDate } };
                    if (data.Value == null)
                        calculatedDataPoints.Add("DataPoint", "Cannot Calculate, Not Enough Data");
                    else
                    {
                        calculatedDataPoints.Add("DataPoint", GetDataPointForm((decimal)data.Value, data.Type));
                        var result = _repository.CreateNewHistoricalDataPoint(DateTime.Parse(data.ApplicableDate), (decimal)data.Value, mid, true);
                        //change for when all are successful
                        if (result.ToLower().Contains("success"))
                            success.Add(result);
                        else
                            error.Add(result);
                    }
                    datapointList.Add(calculatedDataPoints);
                }
                error.Add(" Already Exist!");
                ParseOutCreateHistoricalDataPointResults(error, success);
            }
            SetViewDataMeasureId(Convert.ToInt32(mIdString));
            return Json(datapointList, JsonRequestBehavior.DenyGet);
        }

        //Adding historical data points if they do not already exist
        [HttpPost]
        public ActionResult AddHistoricalData()
        {
            var start = Request["historyDateFrom"];
            var end = Request["historyDateTo"];
            var mIdString = Request["measurementID"];
            string dates = new JavaScriptSerializer().Serialize(GetDates(start, end).Data);
            List<string> dateList = JsonConvert.DeserializeObject<List<string>>(dates);
            List<string> error = new List<string> { "Data Point for " };
            List<string> success = new List<string> { "You Have Added Historical Data Points for: " };

            for (int i = 0; i < dateList.Count; i++)
            {
                var datapointString = Request["newDataPointValue-" + i].Replace("$", "").Replace(",", "").Replace("%", "");
                if (ValidateNewValue(datapointString) && ValidateMeasurement(mIdString))
                {
                    var date = DateTime.Parse(dateList[i]);
                    var datapoint = decimal.Parse(datapointString);
                    var mId = int.Parse(mIdString);
                    //save to db id doesn't exist
                    var addedResult = _repository.CreateNewHistoricalDataPoint(date, datapoint, mId);
                    //change for when all are successful
                    if (addedResult.ToLower().Contains("success"))
                        success.Add(addedResult);
                    else
                        error.Add(addedResult);
                }
            }
            error.Add(" Already Exist!");
            ParseOutCreateHistoricalDataPointResults(error, success);
            SetViewDataMeasureId(Convert.ToInt32(mIdString));
            return RedirectToAction("Index");
        }

        //Edits a single data point that was Edited
        [HttpPost]
        public ActionResult Edit()
        {
            var mId = Request["mId"];
            var datapointIdString = Request["datapointID"];
            var newValueString = Request["txtValue"].Replace("$", "").Replace(",", "").Replace("%", "");
            var reason = Request["editReason"];

            ValidateMeasurement(mId);

            if (!ValidateReason(reason))
                return RedirectToAction("Index");

            if (ValidateDataPoint(datapointIdString) && ValidateNewValue(newValueString))
            {
                var newValue = decimal.Parse(newValueString);
                var datapointId = int.Parse(datapointIdString);

                var username = User.Identity.Name.Substring(0,3);
                var oldValue = _repository.GetDataPointValue(datapointId);
                if (!oldValue.HasValue)
                    oldValue = 0;

                if (!_repository.InsertDatapointEditAudit(username, (decimal)oldValue, newValue, reason, datapointId))
                {
                    TempData["Error"] = "Database error, failed to audit";
                    return RedirectToAction("Index");
                }

                var updateResult = _repository.UpdateDatapoint(datapointId, newValue);
                if (updateResult)
                {
                    TempData["success"] = "You have successfully updated the datapoint!";
                }
                else
                {
                    TempData["error"] = "Database error, please try again later!";
                }
            }
            SetViewDataMeasureId(Convert.ToInt32(mId));
            return RedirectToAction("Index");
        }

        //Sets the measure id to the view data
        public void SetViewDataMeasureId(int measurementId)
        {
            TempData["measureId"] = measurementId;
        }

        //Validate that a new value is a decimal
        private bool ValidateNewValue(string newValueString)
        {
            decimal newValue;
            var newValueIsDecimal = decimal.TryParse(newValueString, out newValue);
            if (!newValueIsDecimal)
                TempData["error"] = "Invalid new value entered!";
            return newValueIsDecimal;
        }

        //Check if data point is an integer
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

        //Check if measurement id is a integer
        public bool ValidateMeasurement(string measurementIdString)
        {
            int measurementId;
            var measurementIdIsInt = int.TryParse(measurementIdString, out measurementId);
            var isValidMeasurementId = false;
            if (measurementIdIsInt)
                isValidMeasurementId = _repository.IsMeasurementIdValid(measurementId);

            if (!isValidMeasurementId)
                TempData["error"] = "Invalid measurement id";

            return isValidMeasurementId;
        }

        public bool ValidateDateRange(string fromDate, string toDate)
        {
            var isValidDateRange = !(string.IsNullOrWhiteSpace(fromDate) || string.IsNullOrWhiteSpace(toDate));
            return isValidDateRange;
        }

        // Converts the item to the form of what data type the item needs to be
        private string GetDataPointForm(decimal item, int dataType, string roundingPlaces = ".00")
        {
            string itemConverted;
            switch (dataType)
            {
                case 1://#
                    itemConverted = item.ToString("###,###,###,##0" + roundingPlaces);
                    break;
                case 2://$
                    itemConverted = item.ToString("$###,###,###,##0" + roundingPlaces);
                    break;
                case 3://%
                    itemConverted = item.ToString("###,###,###,##0" + roundingPlaces) + "%";
                    break;
                default:
                    itemConverted = "There is no data type";
                    break;
            }
            return itemConverted;
        }

        //Method for parsing out the results for the TempData to display to user
        private void ParseOutCreateHistoricalDataPointResults(List<string> error, List<string> success)
        {
            if (error.Count > 2)
                for (int i = 0; i < error.Count; i++)
                {
                    if (i == 1)
                        TempData["Error"] += error[i].Replace(",", "");
                    else
                        TempData["Error"] += error[i];
                }
            if (success.Count > 1)
                for (int i = 0; i < success.Count; i++)
                {
                    if (i == 1)
                        TempData["Success"] += success[i].Replace("success,", "");
                    else
                        TempData["Success"] += success[i].Replace("success", "");
                }
        }

        //Check if Reason is the right length
        public bool ValidateReason(string reason)
        {   //bool logic
            var reasonIsBlank = reason.IsNullOrWhiteSpace();
            var reasonIsTooLong = false;
            if (!reasonIsBlank)
                reasonIsTooLong = (reason.Length > 256);
            var reasonIsValid = !(reasonIsBlank || reasonIsTooLong);

            //set error messages (if needed)
            if (reasonIsBlank)
                TempData["Error"] = "Reason cannot be blank!";
            if (reasonIsTooLong)
                TempData["Error"] = "Reason must be shorter than 256 characters!";

            //return
            return reasonIsValid;
        }
    }
}