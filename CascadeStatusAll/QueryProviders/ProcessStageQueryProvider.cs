using Microsoft.Xrm.Sdk.Query;

namespace CG.Plugins.CascadeStatusAll.QueryProviders
{
    public static class ProcessStageQueryProvider
    {
        public static QueryExpression GetBpfStagesByProcessId(
            string processId
            )
        {

            return new QueryExpression("processstage")
            {
                ColumnSet = new ColumnSet("processstageid", "stagename", "stagecategory", "stepname", "workflowid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("workflowid", ConditionOperator.Equal, processId)
                    }
                },
                Orders =
                {
                    new OrderExpression("stagename", OrderType.Ascending)
                }
            };

        }
    }


}
