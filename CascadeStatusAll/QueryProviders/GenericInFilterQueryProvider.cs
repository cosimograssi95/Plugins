using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;
namespace CG.Plugins.CascadeStatusAll.QueryProviders
{
    public static class GenericInFilterQueryProvider
    {
        public static QueryExpression GetMultipleRecords(
            string entityLogicalName,
            string attributeLogicalName,
            string conditionAttributeLogicalName,
            Guid[] recordsGuidArray
            )
        {

            return new QueryExpression(entityLogicalName)
            {
                ColumnSet = new ColumnSet(attributeLogicalName, "statecode", "statuscode", "modifiedon"),
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


        }

        public static QueryExpression GetMultipleRecordsFromManyToMany(
           string entityLogicalName,
           string attributeLogicalName,
           string intersectEntityName,
           string linkFromEntityAttribute,
           string linkToEntityAttribute,
           string linkToRelatedEntityAttribute,
           Guid[] recordsGuidArray
       )
        {
            QueryExpression query = new QueryExpression(entityLogicalName)
            {
                ColumnSet = new ColumnSet(attributeLogicalName, "statecode", "statuscode", "modifiedon"),
                Criteria = new FilterExpression(LogicalOperator.And)
            };

            LinkEntity link = query.AddLink(intersectEntityName, linkFromEntityAttribute, linkToEntityAttribute);
            link.LinkCriteria = new FilterExpression(LogicalOperator.And);
            link.LinkCriteria.AddCondition(linkToRelatedEntityAttribute, ConditionOperator.In, recordsGuidArray.Cast<object>().ToArray());

            return query;
        }
    }
}



