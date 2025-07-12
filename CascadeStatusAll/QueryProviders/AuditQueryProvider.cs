using Microsoft.Xrm.Sdk.Query;
using System;
namespace CG.Plugins.CascadeStatusAll.QueryProviders
{
    public static class AuditQueryProvider
    {
        public static QueryExpression GetAudits(string attributemask, Guid[] recordsGuidArray)
        {
            return new QueryExpression("audit")
            {
                ColumnSet = new ColumnSet(true),
                Criteria = new FilterExpression(LogicalOperator.And)
                {
                    Conditions =
                    {
                        new ConditionExpression(
                            "objectid",
                            ConditionOperator.In,
                            recordsGuidArray
                            )
                    },
                    Filters =
                    {
                        new FilterExpression
                        {
                            FilterOperator = LogicalOperator.Or,
                            Conditions =
                            {
                                new ConditionExpression("action",ConditionOperator.Equal,1),
                                new ConditionExpression("action",ConditionOperator.Equal,2)
                            }
                        },
                        new FilterExpression
                        {
                            FilterOperator = LogicalOperator.And,
                            Conditions =
                            {
                                new ConditionExpression("attributemask",ConditionOperator.Like,attributemask)
                            }
                        },
                    }
                },
                Orders =
                {
                    new OrderExpression("createdon",OrderType.Descending)
                }
            };

        }
    }
}
