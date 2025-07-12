using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;
namespace CG.Plugins.CascadeStatusAll.QueryProviders
{
    public static class BusinessProcessFlowQueryProvider
    {
        public static QueryExpression GetBpfRecords(
            string entityLogicalName,
            string attributeLogicalName,
            string conditionAttributeLogicalName,
            Guid[] recordsGuidArray,
            string referencedEntity,
            string referencedAttribute,
            string referencingAttribute
            )
        {

            QueryExpression queryExpression = new QueryExpression(entityLogicalName)
            {
                ColumnSet = new ColumnSet("processid", "activestageid", "statecode", "statuscode"),
                Criteria = new FilterExpression(LogicalOperator.And)
                {
                    Conditions =
                    {
                        new ConditionExpression(
                            conditionAttributeLogicalName,
                            ConditionOperator.In,
                            recordsGuidArray.Cast<object>().ToArray()
                            )
                    }
                }
            };

            LinkEntity stageLink = new LinkEntity
            {
                LinkFromEntityName = entityLogicalName,
                LinkFromAttributeName = "activestageid",
                LinkToEntityName = "processstage",
                LinkToAttributeName = "processstageid",
                EntityAlias = "activestage",
                Columns = new ColumnSet("stagename", "processstageid")
            };
            queryExpression.LinkEntities.Add(stageLink);

            LinkEntity parentLink = new LinkEntity
            {
                LinkFromEntityName = entityLogicalName,
                LinkFromAttributeName = referencingAttribute,
                LinkToEntityName = referencedEntity,
                LinkToAttributeName = referencedAttribute,
                EntityAlias = "parent",
                Columns = new ColumnSet(referencedAttribute, "statecode")
            };
            queryExpression.LinkEntities.Add(parentLink);

            return queryExpression;

        }
    }
}