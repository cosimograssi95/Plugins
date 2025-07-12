using Microsoft.Xrm.Sdk.Query;

namespace CG.Plugins.CascadeStatusAll.QueryProviders
{
    public static class ProcessQueryProvider
    {
        public static QueryExpression GetWorkflowById(
            string processid
            )
        {

            return new QueryExpression("workflow")
            {
                ColumnSet = new ColumnSet("workflowid", "clientdata"),
                Criteria = new FilterExpression(LogicalOperator.And)
                {
                    Conditions =
                    {
                        new ConditionExpression(
                            attributeName: "workflowid",
                            conditionOperator: ConditionOperator.Equal,
                            value: processid
                            )
                    }
                }
            };

        }
        public static QueryExpression GetWorkflowByEntityLogicalName(
            string entityLogicalName
            )
        {

            return new QueryExpression("workflow")
            {
                ColumnSet = new ColumnSet("workflowid"),
                TopCount = 1,
                Criteria =
                {
                    Conditions =
                    {
                        new ConditionExpression("type", ConditionOperator.Equal, 1),
                        new ConditionExpression("category", ConditionOperator.Equal, 4),
                        new ConditionExpression("uniquename", ConditionOperator.Equal, entityLogicalName)
                    }
                }
            };
        }
    }
}
