using Microsoft.Xrm.Sdk;
using System;

namespace LeaveManagement.Plugins
{
    /// <summary>
    /// Validates the dates of a Semesteransökan in Dataverse, whichever way it is saved
    /// (canvas app, model-driven app, flow or API).
    /// Steps: Create (PreValidation) and Update (PreOperation, pre-image "PreImage"
    /// with smo_startdatum and smo_slutdatum, filtering attributes on the same columns).
    /// </summary>
    public class ValidateLeaveRequest : PluginBase
    {
        public ValidateLeaveRequest(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(ValidateLeaveRequest))
        {
        }

        protected override void ExecuteDataversePlugin(ILocalPluginContext localPluginContext)
        {
            if (localPluginContext == null)
            {
                throw new ArgumentNullException(nameof(localPluginContext));
            }

            var context = localPluginContext.PluginExecutionContext;

            // Get the row being saved (Target). Stop if it isn't a leave request.
            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity))
            {
                return;
            }

            var target = (Entity)context.InputParameters["Target"];

            if (target.LogicalName != "smo_semesteransokan")
            {
                return;
            }

            // Get the row as it was before the change. Only exists on Update.
            Entity preImage = null;

            if (context.PreEntityImages.Contains("PreImage"))
            {
                preImage = context.PreEntityImages["PreImage"];
            }

            // The dates as they will be if the save goes through.
            var startDate = GetDate(target, preImage, "smo_startdatum");
            var endDate = GetDate(target, preImage, "smo_slutdatum");

            localPluginContext.Trace($"Start date: {startDate}, End date: {endDate}");

            // The rules. Throwing an exception stops the save and shows the message.
            if (startDate == null || endDate == null)
            {
                throw new InvalidPluginExecutionException("Både startdatum och slutdatum måste anges.");
            }

            if (endDate < startDate)
            {
                throw new InvalidPluginExecutionException("Slutdatum kan inte vara före startdatum.");
            }

            if (startDate.Value.Year != endDate.Value.Year)
            {
                throw new InvalidPluginExecutionException(
                    "En ansökan får inte gå över årsskiftet. Skicka en ansökan för varje år.");
            }
        }

        // Use the new value if the column is being changed (Target), otherwise the old value (pre-image).
        private static DateTime? GetDate(Entity target, Entity preImage, string columnName)
        {
            if (target.Contains(columnName))
            {
                return target.GetAttributeValue<DateTime?>(columnName);
            }

            if (preImage != null)
            {
                return preImage.GetAttributeValue<DateTime?>(columnName);
            }

            return null;
        }
    }
}
