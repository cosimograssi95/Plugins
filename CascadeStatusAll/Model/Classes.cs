using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;

namespace CG.Plugins.CascadeStatusAll.Model
{
    public class RelationshipObj
    {
        public string RelationshipType { get; set; }

        // 1:N properties
        public string ReferencingEntity { get; set; }
        public string ReferencingAttribute { get; set; }
        public string ReferencingEntityNavigationPropertyName { get; set; }
        public string ReferencedEntity { get; set; }
        public string ReferencedAttribute { get; set; }
        public string ReferencedEntityNavigationPropertyName { get; set; }

        // N:N properties
        public string Entity1LogicalName { get; set; }
        public string Entity1IntersectAttribute { get; set; }
        public string Entity2LogicalName { get; set; }
        public string Entity2IntersectAttribute { get; set; }
        public string IntersectEntityName { get; set; }
        public string SchemaName { get; set; }
    }
    public class EntityRelationshipObj
    {
        public string entitySetName { get; set; }
        public List<RelationshipObj> relationshipObj { get; set; }
    }
    public class ChangeDataObj
    {
        public string logicalName { get; set; }
        public string oldValue { get; set; }
        public string newValue { get; set; }
    }
    public class AuditObj
    {
        public ChangeDataObj[] changedAttributes { get; set; }
        public string createdon { get; set; }
        public Guid auditId { get; set; }
        public Guid objectId { get; set; }
    }
    public class EntityMetaDataObj
    {
        public string primaryIdAttribute { get; set; }
    }
    public class BPFRelationshipObj
    {
        public string parentEntityName { get; set; }
        public string bpfEntityName { get; set; }
        public string[] lastBpfPhasesNames { get; set; }
    }
    public class UpdatedRecordObj
    {
        public Guid recordId { get; set; }
        public string entityName { get; set; }
        public OptionSetValue status { get; set; }
    }

    public class AttributeMetaDataObj
    {
        public int columnNumber { get; set; }
        public int optionValue { get; set; }
    }

    public class ResponseRecordObj
    {
        public Guid recordId { get; set; }
        public int statecode { get; set; }
        public int statuscode { get; set; }
        public string entityLogicalName { get; set; }
        public string entitySetName { get; set; }
    }
    public class ResponseObj
    {
        public List<ResponseRecordObj> recordList { get; set; }
        public string entityLogicalName { get; set; }
    }
    public class StageObj
    {
        public string id { get; set; }
        public string name { get; set; }
        public string nextStageId { get; set; }
        public string innerData { get; set; }
    }
}
