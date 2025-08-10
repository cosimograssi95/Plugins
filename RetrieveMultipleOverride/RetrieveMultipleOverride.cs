using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace CG.Plugins.RetrieveMultipleOverride
{
    /// <summary>
    /// Plugin development guide: https://docs.microsoft.com/powerapps/developer/common-data-service/plug-ins
    /// Best practices and guidance: https://docs.microsoft.com/powerapps/developer/common-data-service/best-practices/business-logic/
    /// </summary>
    public class RetrieveMultipleOverride : PluginBase
    {
        public RetrieveMultipleOverride(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(RetrieveMultipleOverride))
        {
            // TODO: Implement your custom configuration handling
            // https://docs.microsoft.com/powerapps/developer/common-data-service/register-plug-in#set-configuration-data
        }

        protected override void ExecuteDataversePlugin(ILocalPluginContext localPluginContext)
        {
            if (localPluginContext == null)
            {
                throw new ArgumentNullException(nameof(localPluginContext));
            }

            #region getContext
            IPluginExecutionContext context = localPluginContext.PluginExecutionContext;

            IOrganizationService service = ((IOrganizationServiceFactory)localPluginContext.ServiceProvider.GetService(typeof(IOrganizationServiceFactory))).CreateOrganizationService(null);

            ITracingService tracingService = localPluginContext.TracingService;
            #endregion

            try
            {
                if (context.Mode != 0 || context.Stage != 20 || !context.MessageName.Equals("RetrieveMultiple", StringComparison.OrdinalIgnoreCase) || context.Depth != 1)
                    return;

                if (!context.InputParameters.Contains("Query") || !(context.InputParameters["Query"] is FetchExpression fetchExpression))
                    return;


                XDocument fetchXmlDocument = XDocument.Parse(fetchExpression.Query);
                if (fetchXmlDocument.Descendants("link-entity").FirstOrDefault() == null) return;

                XElement linkEntityElement = fetchXmlDocument.Descendants("link-entity").FirstOrDefault();
                if (linkEntityElement == null) return;

                string linkEntityName = linkEntityElement.Attribute("name").Value;
                if (linkEntityName != context.PrimaryEntityName) return;

                IEnumerable<XElement> linkEntityFilterElements = linkEntityElement.Descendants("filter");

                XElement linkEntitySelfReferentialCondition =
                    linkEntityFilterElements
                        .Descendants("condition")
                        .Where(condition =>
                            condition.Attribute("attribute")?.Value.StartsWith(context.PrimaryEntityName, StringComparison.OrdinalIgnoreCase) == true)
                        .FirstOrDefault();

                if (linkEntitySelfReferentialCondition == null) return;

                if (!Guid.TryParse(linkEntitySelfReferentialCondition.Attribute("value")?.Value, out Guid parentRecordId))
                    return;


                #region customLogic
                Entity parent = service.Retrieve(context.PrimaryEntityName, parentRecordId, new ColumnSet("new_tablefetchbcategory"));

                OptionSetValue categoryOption = parent.GetAttributeValue<OptionSetValue>("new_tablefetchbcategory");
                if (categoryOption == null)
                    return;
                #endregion


                #region customFilter

                XElement customFilter = new XElement("filter",

                    new XAttribute("type", "and"),

                    new XElement("condition",
                        new XAttribute("attribute", linkEntitySelfReferentialCondition.Attribute("attribute").Value),
                        new XAttribute("operator", "ne"),
                        new XAttribute("value", parentRecordId)
                    ),

                    new XElement("condition",
                        new XAttribute("attribute", "new_tablefetchbcategory"),
                        new XAttribute("operator", "eq"),
                        new XAttribute("value", categoryOption.Value.ToString())
                    )
                );

                #endregion

                linkEntityElement.ReplaceWith(customFilter);

                fetchExpression.Query = fetchXmlDocument.ToString();

            }
            catch (Exception ex)
            {
                string errorMsg = ex.InnerException != null ? $"{ex.Message} Inner Exception: {ex.InnerException.Message}" : ex.Message;
                throw new Exception(errorMsg, ex);
            }
        }
    }
}