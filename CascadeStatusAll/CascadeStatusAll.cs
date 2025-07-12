using CG.Plugins.CascadeStatusAll.Model;
using CG.Plugins.CascadeStatusAll.QueryProviders;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace CG.Plugins.CascadeStatusAll
{
    /// <summary>
    /// Plugin development guide: https://docs.microsoft.com/powerapps/developer/common-data-service/plug-ins
    /// Best practices and guidance: https://docs.microsoft.com/powerapps/developer/common-data-service/best-practices/business-logic/
    /// 
    ///
    /// DESCRIPTION:
    /// This Custom API implements Cascade All functionality for Status and Status Reason, starting from a set of parent records.
    /// It doesn't update any record, but retrieves all the records, and their states, required to perform a massive update via other means ( Power Automate ).
    /// It takes "recordsGUID" (CSV) as input and based on "shouldUpdateParent" flag decide if it needs to retrieve those parent record Status/Status Reason.
    ///
    /// If "shouldRestorePreviousStatus" is False, the values specified for "statusLabel" and "statusReasonLabel" are used to set every record state for each entity, outside of
    /// bpf entities which have a different logic.
    /// After parent records are retrieved, the API retrieves all child relationships that match "publisherPrefix" and recursively call <see cref="GetChildrenState"/> on child entities
    /// to get their Status/Status Reason.
    /// Note: for simple scenarios where you just need to cascade the default "Active"/"Inactive" status reasons you can ignore "shouldRestorePreviousStatus" flag and leave it to False.
    /// Note: you can use "entitiesLogicalNamesToExclude" (CSV) to exclude particular entities from the Cascading Process by provinding their logical name. This will also exclude all
    /// of those entities children of any level if those are not children of any other processed entity.
    /// Note: you can use "entitiesLogicalNamesToInclude" (CSV) to include particular entities that do not match the publisherPrefix 
    /// in the Cascading Process by provinding their logical name.
    /// Note: you can use "entitiesLogicalNamesToRecalculate" (CSV) to ensure that particular entities will always be recalculated instead of being ignored after being found for the first time.
    /// This help to solve the issue that a child entity might have multiple parents, if we do not include the entity in this parameter only the records coming from the relationtionship of the
    /// first parent will be processed.
    ///
    /// If "shouldCascadeRecalculation" is True, all children entities of any entities listed as "entitiesLogicalNamesToRecalculate" will be included as an entity to be recalculated.
    /// Only entities listed as "entitiesLogicalNamesToRecalculate" will be recalculated multiple times otherwise.
    /// 
    /// If "shouldRestorePreviousStatus" is True the methods <see cref="CalculateStateCodeOptionSet"/> and <see cref="CalculateStatusCodeOptionSet"/> will try to retrieve
    /// the last Status/Status Reason value from the last Create/Update Audit of those records.
    /// Note: if it doesn't find any audit or just the Creation Audit it simply uses the values specified for "statusLabel" and "statusReasonLabel".
    /// Note: this feature works correctly if the change in Status Reason for the Parent and the Children is identified by a different Status Reason OptionSet Value.
    /// No audit record is saved if an update doesn't change atleast the Status Reason.
    /// 
    /// If "statusReasonNeverRestored" (CSV) is passed the API will try to look for those status reason labels and exclude them from the process of restoration ( only work in conjuction with
    /// "shouldRestorePreviousStatus" flag ).
    /// This is useful when there are many different point of change for the Status Reason in the process and you want to be sure that the restoration exclude certain type of changes.
    /// (for example when restoring child processes directly it is suggested to exclude parent Status Reasons)
    /// 
    ///
    /// SCENARIO:
    /// Ideally the intended scenario is to have a Parent with N Children and SubChildren.
    /// Let's say that during the process some Children get Deactivated with Status Reason "Standard Deactivation".
    /// At some point you decide to Interrupt the Parent by deactivating it and specifing the Reason as "Parent Interrupted" and
    /// in order to achieve this you call this API with :
    ///
    /// "statusLabel"="Inactive",
    /// "statusReasonLabel"="Parent Interrupted",
    /// "shouldRestorePreviousStatus"="False",
    /// "shouldUpdateParent"="True"
    ///
    /// The outcome is that the parents specified in "recordsGUID" and all of his Children get retrieved with that particular Status Reason.
    /// You can proceed to update the records in the output of this API massively in Power Automate.
    /// At some point you decide to revert this, the Parent needs to be activated again and you would like to restore every previous state,
    /// now you can call this API with :
    ///
    /// "statusLabel"="Active", ( this property generally ignored when "shouldRestorePreviousStatus"="True" except if audits are not found )
    /// "statusReasonLabel"="Active", ( this property generally ignored when "shouldRestorePreviousStatus"="True" except if audits are not found )
    /// "shouldRestorePreviousStatus"="True",
    /// "shouldUpdateParent"="True"
    ///
    /// The outcome is that the parents specified in "recordsGUID" and all of their Children get retrieved with the previous state, so you won't lose
    /// the state of the Children and some of them will remain "Inactive" with Status Reason "Standard Deactivation" which we set previously while others
    /// will be "Active".
    ///
    /// PREREQUISITES:
    /// - Enable Auditing on Environment and wait 12 hours for audit data migration to MongoDB-NOSQL ( for "shouldRestorePreviousStatus" feature flag )
    /// - Enable Auditing on all custom entities you plan to use for your project except BPF entities ( for "shouldRestorePreviousStatus" feature flag )
    /// - Enable Auditing on statecode/statuscode columns of those entities ( for "shouldRestorePreviousStatus" feature flag )
    /// - Set All Children Status Reason Label the same for all Children Entities except BPF entities, the value doesn't matter
    /// - The Parent NEEDS to have the same Status Reason Values Labels of Children except BPF entities, but can use less
    /// (in the example above the Parent doesn't need the "Standard Deactivation" value since the intended use is for standard process deactivation of children.
    /// Children requires the "Parent Interrupted" status reason)
    ///
    /// LIMITATIONS:
    /// - Standard 5000 items limitation for Dataverse queries applies
    /// - Standard 2 minute timeout applies
    /// - If the Audits are cleared, "shouldRestorePreviousStatus" feature flag won't work and "statusLabel","statusReasonLabel" will be applied;
    /// consider setting a data retention policy with the same retention period as the audit one disable the action altogheter if a certain amount of time has passed
    /// since the last change of the record,
    /// this prevent the situation of trying to restore a record that has no audit, atleast in the case of automatic audit deletion, since it will be retained and read only
    /// - The API has several failsafes against spamming:
    /// 1. If the API finds the last audit not to match the current state it assumes the audit to be the one before the last so it tries to restore by using the "newValue"
    /// instead of "oldValue".<see cref="IsLastStateAuditCurrentState"/>
    /// 2. The API checks for the audit to be written to be valid by ensuring that the values it is going to write are different from the current record values.<see cref="IsLastStateAuditValid"/>
    /// 3. If both <see cref="IsLastStateAuditCurrentState"/> and <see cref="IsLastStateAuditValid"/> return false, it will restore the values passed as inputs as fallback.
    /// </summary>

    public class CascadeStatusAll : PluginBase
    {
        public CascadeStatusAll(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(CascadeStatusAll))
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
            var context = localPluginContext.PluginExecutionContext;

            IOrganizationService service = ((IOrganizationServiceFactory)localPluginContext.ServiceProvider.GetService(typeof(IOrganizationServiceFactory))).CreateOrganizationService(null);

            ITracingService tracingService = localPluginContext.TracingService;
            #endregion


            #region getInput
            //Parent concatenated GUID
            string recordsGUID = context.InputParameters["recordsGUID"].ToString();
            //Parent EntityLogicalName
            string entityLogicalName = context.InputParameters["entityLogicalName"].ToString();
            //Publisher Prefix
            string publisherPrefix = context.InputParameters["publisherPrefix"].ToString();
            //Status
            string statusLabel = context.InputParameters["statusLabel"].ToString();
            //Status Reason
            string statusReasonLabel = context.InputParameters["statusReasonLabel"].ToString();
            //Should Update Parent
            bool shouldUpdateParent = (bool)context.InputParameters["shouldUpdateParent"];

            //Should Revert Records previous status/status reason
            // This behaviour only works if you have atleast 2 different status reasons for the state you are trying to revert
            // for example Status "Parent Interrupted" for parent deactivation that cascade over his children
            // and "Standard Deactivation" if during the standard process children gets deactivated normally.
            // This behaviour WILL NOT WORK PROPERLY if you try to restore records to a state with a single status reasons
            // either for active or inactive status depending on what you are trying to restore
            bool shouldRestorePreviousStatus = (bool)context.InputParameters["shouldRestorePreviousStatus"];

            //Entities included in the cascading process logical name (OPTIONAL)
            // use this parameter to include entities that do not have the same publisher prefix
            string entitiesLogicalNamesToInclude = string.Empty;

            if (context.InputParameters["entitiesLogicalNamesToInclude"] != null)
            {
                entitiesLogicalNamesToInclude = context.InputParameters["entitiesLogicalNamesToInclude"].ToString();
            }

            //Entities excluded from the cascading process logical name (OPTIONAL)
            string entitiesLogicalNamesToExclude = string.Empty;

            if (context.InputParameters["entitiesLogicalNamesToExclude"] != null)
            {
                entitiesLogicalNamesToExclude = context.InputParameters["entitiesLogicalNamesToExclude"].ToString();
            }
            //Entities that will always be recalculated, even if they already appeared in the cascading process (OPTIONAL)
            string entitiesLogicalNamesToRecalculate = string.Empty;

            if (context.InputParameters["entitiesLogicalNamesToRecalculate"] != null)
            {
                entitiesLogicalNamesToRecalculate = context.InputParameters["entitiesLogicalNamesToRecalculate"].ToString();
            }

            //Should Cascade Recalculation
            // When this flag is on child entities of entities specified in "entitiesLogicalNamesToRecalculate", will be automatically
            // considered for recalculation.
            // When off, only specified entities will be recalculated multiple times.
            bool shouldCascadeRecalculation = (bool)context.InputParameters["shouldCascadeRecalculation"];

            //Status Reasons that will be avoided in the restoration process (OPTIONAL)
            string statusReasonNeverRestored = string.Empty;

            if (context.InputParameters["statusReasonNeverRestored"] != null)
            {
                statusReasonNeverRestored = context.InputParameters["statusReasonNeverRestored"].ToString();
            }

            #endregion

            #region initLists
            List<ResponseObj> responseObj = new List<ResponseObj>();
            ConcurrentBag<UpdatedRecordObj> updatedParentRecordsBag = new ConcurrentBag<UpdatedRecordObj>();
            ConcurrentBag<string> statusReasonNeverRestoredBag = new ConcurrentBag<string>();
            ConcurrentBag<string> entitiesLogicalNamesToExcludeBag = new ConcurrentBag<string>();
            ConcurrentBag<string> entitiesLogicalNamesToIncludeBag = new ConcurrentBag<string>();
            ConcurrentBag<string> entitiesLogicalNamesProcessedBag = new ConcurrentBag<string>();
            ConcurrentBag<string> entitiesLogicalNamesToRecalculateBag = new ConcurrentBag<string>();
            ConcurrentQueue<Exception> exceptionsQueue = new ConcurrentQueue<Exception>();
            #endregion
            try
            {
                #region setLists
                ProcessEntityLogicalNames(
                    entitiesLogicalNamesToInclude,
                    "include",
                    publisherPrefix,
                    entitiesLogicalNamesToIncludeBag,
                    exceptionsQueue);

                ProcessEntityLogicalNames(
                    entitiesLogicalNamesToExclude,
                    "exclude",
                    publisherPrefix,
                    entitiesLogicalNamesToExcludeBag,
                    exceptionsQueue);

                ProcessEntityLogicalNames(
                    entitiesLogicalNamesToRecalculate,
                    "recalculate",
                    publisherPrefix,
                    entitiesLogicalNamesToRecalculateBag,
                    exceptionsQueue);


                #endregion

                GetChildrenState(
                    service,
                    recordsGUID,
                    entityLogicalName,
                    publisherPrefix,
                    statusLabel,
                    statusReasonLabel,
                    shouldUpdateParent,
                    shouldRestorePreviousStatus,
                    true,
                    entitiesLogicalNamesToExcludeBag,
                    entitiesLogicalNamesToIncludeBag,
                    entitiesLogicalNamesProcessedBag,
                    entitiesLogicalNamesToRecalculateBag,
                    shouldCascadeRecalculation,
                    statusReasonNeverRestoredBag,
                    statusReasonNeverRestored,
                    exceptionsQueue,
                    updatedParentRecordsBag,
                    "",
                    tracingService,
                    responseObj);

                List<ResponseRecordObj> allResponseList = responseObj
                .Where(r => r.recordList != null)
                .SelectMany(r => r.recordList)
                .ToList();

                List<Entity> responseList = allResponseList.Select(res => new Entity()
                {
                    Attributes =
                    {
                    { "recordId", res.recordId },
                    { "statecode", res.statecode },
                    { "statuscode", res.statuscode },
                    { "entityLogicalName", res.entityLogicalName },
                    { "entitySetName",res.entitySetName }
                    }
                }).ToList();
                EntityCollection responseCollection = new EntityCollection(responseList);

                context.OutputParameters["response"] = responseCollection;
            }
            catch (AggregateException ae)
            {
                #region throwEx
                List<Exception> exceptionList = new List<Exception>();
                string exceptionMessage = string.Empty;


                foreach (var ex in ae.Flatten().InnerExceptions)
                {
                    exceptionList.Add(ex);
                    exceptionMessage += ex.Message + Environment.NewLine;
                }
                throw new AggregateException(exceptionMessage, exceptionList);

                #endregion
            }
        }

        #region methodsDef
        // recursive function
        private void GetChildrenState(
            IOrganizationService service,
            string recordsGUID,
            string entityLogicalName,
            string publisherPrefix,
            string statusLabel,
            string statusReasonLabel,
            bool shouldUpdateParent,
            bool shouldRestorePreviousStatus,
            bool isParentEntity,
            ConcurrentBag<string> entitiesLogicalNamesToExcludeBag,
            ConcurrentBag<string> entitiesLogicalNamesToIncludeBag,
            ConcurrentBag<string> entitiesLogicalNamesProcessedBag,
            ConcurrentBag<string> entitiesLogicalNamesToRecalculateBag,
            bool shouldCascadeRecalculation,
            ConcurrentBag<string> statusReasonNeverRestoredBag,
            string statusReasonNeverRestored,
            ConcurrentQueue<Exception> exceptionsQueue,
            ConcurrentBag<UpdatedRecordObj> updatedParentRecordsBag,
            string relatedEntityLogicalName,
            ITracingService tracingService,
            List<ResponseObj> responseObj)
        {

            EntityRelationshipObj entityRelationshipObj = GetRelatedEntities(service, entityLogicalName, exceptionsQueue, tracingService);

            // this filters over relationships are very important
            // we check for publisher prefix or entities to include for both type of relationships
            // we make sure that, in case of an N:N relationship we have not already processed the "relatedEntityLogicalName"
            // this check prevents OOTB N:N entities to infinitely references each other
            List<RelationshipObj> customEntitiesList = entityRelationshipObj.relationshipObj
                 .Where(rel => !string.IsNullOrEmpty(rel.ReferencingEntity) &&
                 (rel.ReferencingEntity.StartsWith(publisherPrefix + "_") || IsStringInsideBag(entitiesLogicalNamesToIncludeBag, rel.ReferencingEntity))

                 || !string.IsNullOrEmpty(rel.Entity1LogicalName) &&
                 (rel.Entity1LogicalName.StartsWith(publisherPrefix + "_") || IsStringInsideBag(entitiesLogicalNamesToIncludeBag, rel.Entity1LogicalName)) &&
                 !(rel.Entity1LogicalName == relatedEntityLogicalName && IsStringInsideBag(entitiesLogicalNamesProcessedBag, rel.Entity1LogicalName)) &&
                 !string.IsNullOrEmpty(rel.Entity2LogicalName) &&
                 (rel.Entity2LogicalName.StartsWith(publisherPrefix + "_") || IsStringInsideBag(entitiesLogicalNamesToIncludeBag, rel.Entity2LogicalName)) &&
                 !(rel.Entity2LogicalName == relatedEntityLogicalName && IsStringInsideBag(entitiesLogicalNamesProcessedBag, rel.Entity2LogicalName))

                 )
                .ToList();

            string parentEntitySetName = entityRelationshipObj.entitySetName;

            //this adds the parentEntitySetName to the previously inserted records
            ResponseObj existingResponse = responseObj.FirstOrDefault(el => el.entityLogicalName == entityLogicalName);

            if (existingResponse != null)
            {
                List<ResponseRecordObj> records = existingResponse.recordList;
                existingResponse.recordList = records.Select(el => new ResponseRecordObj()
                {
                    recordId = el.recordId,
                    statecode = el.statecode,
                    statuscode = el.statuscode,
                    entityLogicalName = el.entityLogicalName,
                    entitySetName = parentEntitySetName
                }
                    ).ToList();
            }


            if (!customEntitiesList.Any() && isParentEntity)
            {
                InvalidPluginExecutionException invalidPublisherPrefixPluginlException =
                    new InvalidPluginExecutionException(
                        OperationStatus.Failed, "Processing of entity " + entityLogicalName + " failed with error message : Invalid publisherPrefix provided");
                exceptionsQueue.Enqueue(invalidPublisherPrefixPluginlException);
                throw invalidPublisherPrefixPluginlException;
            }
            bool shouldParentBeExcluded = IsStringInsideBag(entitiesLogicalNamesToExcludeBag, entityLogicalName);
            bool isParentAlreadyProcessed = IsStringInsideBag(entitiesLogicalNamesProcessedBag, entityLogicalName);
            bool shouldParentBeRecalculated = IsStringInsideBag(entitiesLogicalNamesToRecalculateBag, entityLogicalName);
            if (shouldUpdateParent && !shouldParentBeExcluded && (!isParentAlreadyProcessed || shouldParentBeRecalculated))
            {
                EntityMetaDataObj parentEntityMetadata = GetEntityMetadata(service, entityLogicalName, tracingService);
                string primaryIdNameParent = parentEntityMetadata.primaryIdAttribute;
                EntityCollection parentCollection = RetrieveMultipleRecords(
                    service,
                    entityLogicalName,
                    primaryIdNameParent,
                    primaryIdNameParent,
                    recordsGUID,
                    exceptionsQueue,
                    tracingService);
                if (!parentCollection.Entities.Any())
                {
                    InvalidPluginExecutionException parentNotFoundException =
                        new InvalidPluginExecutionException(
                            OperationStatus.Failed, "Processing of entity " + entityLogicalName + " failed with error message : No parent records found");
                    exceptionsQueue.Enqueue(parentNotFoundException);
                    throw parentNotFoundException;
                }
                List<ResponseRecordObj> records = GetRecordsState(
                    service,
                    parentCollection,
                    statusLabel,
                    statusReasonLabel,
                    shouldRestorePreviousStatus,
                    primaryIdNameParent,
                    exceptionsQueue,
                    updatedParentRecordsBag,
                    statusReasonNeverRestoredBag,
                    statusReasonNeverRestored,
                    customEntitiesList,
                    tracingService,
                    responseObj);
                responseObj.Add(new ResponseObj()
                {
                    recordList = records.Select(el => new ResponseRecordObj()
                    {
                        recordId = el.recordId,
                        statecode = el.statecode,
                        statuscode = el.statuscode,
                        entityLogicalName = el.entityLogicalName,
                        entitySetName = parentEntitySetName
                    }
                    ).ToList(),
                    entityLogicalName = entityLogicalName
                });

                entitiesLogicalNamesProcessedBag.Add(entityLogicalName);

            }

            // parallel for each not supported
            // https://learn.microsoft.com/en-us/power-apps/developer/data-platform/best-practices/business-logic/do-not-use-parallel-execution-in-plug-ins
            foreach (RelationshipObj rel in customEntitiesList)
            {
                string relationshipType = rel.RelationshipType;
                string childEntityLogicalName = string.Empty;
                // 1:N properties
                string entityReferencingAttribute = string.Empty;

                // N:N properties
                string linkFromEntityAttribute = string.Empty;
                string linkToEntityAttribute = string.Empty;
                string relatedNNEntityLogicalName = string.Empty;
                string intersectEntityName = string.Empty;

                if (relationshipType == "OneToMany")
                {
                    string entityReferencingEntity = rel.ReferencingEntity.ToString();
                    entityReferencingAttribute = rel.ReferencingAttribute.ToString();

                    childEntityLogicalName = entityReferencingEntity;
                }
                if (relationshipType == "ManyToMany")
                {
                    string entity1LogicalName = rel.Entity1LogicalName.ToString();
                    string entity2LogicalName = rel.Entity2LogicalName.ToString();
                    intersectEntityName = rel.IntersectEntityName;

                    childEntityLogicalName = entity1LogicalName == entityLogicalName ? entity2LogicalName : entity1LogicalName;
                    relatedNNEntityLogicalName = entity1LogicalName == entityLogicalName ? entity1LogicalName : entity2LogicalName;

                    if (shouldParentBeRecalculated && shouldCascadeRecalculation && !IsStringInsideBag(entitiesLogicalNamesToRecalculateBag, childEntityLogicalName))
                        entitiesLogicalNamesToRecalculateBag.Add(childEntityLogicalName);

                    if (rel.Entity1LogicalName == childEntityLogicalName)
                    {
                        linkFromEntityAttribute = rel.Entity1IntersectAttribute;
                        linkToEntityAttribute = rel.Entity2IntersectAttribute;
                    }
                    else
                    {
                        linkFromEntityAttribute = rel.Entity2IntersectAttribute;
                        linkToEntityAttribute = rel.Entity1IntersectAttribute;
                    }
                }
                if (IsStringInsideBag(entitiesLogicalNamesToExcludeBag, childEntityLogicalName))
                {
                    continue;
                }
                if (IsStringInsideBag(entitiesLogicalNamesProcessedBag, childEntityLogicalName) && !IsStringInsideBag(entitiesLogicalNamesToRecalculateBag, childEntityLogicalName))
                {
                    continue;
                }

                entitiesLogicalNamesProcessedBag.Add(childEntityLogicalName);

                EntityMetaDataObj childEntityMetadata = GetEntityMetadata(service, childEntityLogicalName, tracingService);
                string primaryIdNameChild = childEntityMetadata.primaryIdAttribute;
                EntityCollection childCollection = relationshipType == "OneToMany" ?
                    RetrieveMultipleRecords(
                    service,
                    childEntityLogicalName,
                    primaryIdNameChild,
                    entityReferencingAttribute,
                    recordsGUID,
                    exceptionsQueue,
                    tracingService) :
                    RetrieveMultipleRecordsFromManyToMany(
                    service,
                    childEntityLogicalName,
                    primaryIdNameChild,
                    intersectEntityName,
                    linkFromEntityAttribute,
                    linkToEntityAttribute,
                    recordsGUID,
                    exceptionsQueue,
                    tracingService);

                if (!childCollection.Entities.Any())
                {
                    continue;
                }


                List<string> childRecordsGUIDList = childCollection.Entities
                    .Select(entity => entity.Id.ToString()).ToList();
                string childRecordsGUID = string.Join(",", childRecordsGUIDList);



                List<ResponseRecordObj> currentRecordList = GetRecordsState(
                    service,
                    childCollection,
                    statusLabel,
                    statusReasonLabel,
                    shouldRestorePreviousStatus,
                    primaryIdNameChild,
                    exceptionsQueue,
                    updatedParentRecordsBag,
                    statusReasonNeverRestoredBag,
                    statusReasonNeverRestored,
                    customEntitiesList,
                    tracingService,
                    responseObj);
                //if we already processed this entity add delta to exising entity response
                ResponseObj existingChildResponse = responseObj.FirstOrDefault(el => el.entityLogicalName == childEntityLogicalName);
                if (existingChildResponse != null)
                {
                    currentRecordList.AddRange(existingChildResponse.recordList.Where(el2 => !currentRecordList.Any(el1 => el1.recordId == el2.recordId)));
                    existingChildResponse.recordList = currentRecordList;
                }
                else
                {
                    responseObj.Add(new ResponseObj()
                    {
                        recordList = currentRecordList,
                        entityLogicalName = childEntityLogicalName
                    }
                    );
                }


                // recursive call
                GetChildrenState(
                    service,
                    childRecordsGUID,
                    childEntityLogicalName,
                    publisherPrefix,
                    statusLabel,
                    statusReasonLabel,
                    false,
                    shouldRestorePreviousStatus,
                    false,
                    entitiesLogicalNamesToExcludeBag,
                    entitiesLogicalNamesToIncludeBag,
                    entitiesLogicalNamesProcessedBag,
                    entitiesLogicalNamesToRecalculateBag,
                    shouldCascadeRecalculation,
                    statusReasonNeverRestoredBag,
                    statusReasonNeverRestored,
                    exceptionsQueue,
                    updatedParentRecordsBag,
                    relatedNNEntityLogicalName,
                    tracingService,
                    responseObj);




            }


        }
        // Retrieve child entities from a parent "entityName"
        private EntityRelationshipObj GetRelatedEntities(
            IOrganizationService service,
            string entityLogicalName,
            ConcurrentQueue<Exception> exceptionsQueue,
            ITracingService tracingService)
        {
            RetrieveEntityRequest retrieveEntityRequest = new RetrieveEntityRequest
            {
                EntityFilters = EntityFilters.All,
                LogicalName = entityLogicalName,
                RetrieveAsIfPublished = true
            };

            try
            {
                RetrieveEntityResponse retrieveEntityResponse = (RetrieveEntityResponse)service.Execute(retrieveEntityRequest);

                string entitySetName = retrieveEntityResponse.EntityMetadata.EntitySetName;

                List<RelationshipObj> relationshipList = retrieveEntityResponse.EntityMetadata.OneToManyRelationships.Select(rel => new RelationshipObj()
                {
                    RelationshipType = "OneToMany",
                    ReferencingEntity = rel.ReferencingEntity,
                    ReferencingAttribute = rel.ReferencingAttribute,
                    ReferencingEntityNavigationPropertyName = rel.ReferencingEntityNavigationPropertyName,
                    ReferencedEntity = rel.ReferencedEntity,
                    ReferencedAttribute = rel.ReferencedAttribute,
                    ReferencedEntityNavigationPropertyName = rel.ReferencedEntityNavigationPropertyName

                }

        ).ToList();

                List<RelationshipObj> manyToManyRelationships = retrieveEntityResponse.EntityMetadata.ManyToManyRelationships.Select(rel => new RelationshipObj
                {
                    RelationshipType = "ManyToMany",
                    Entity1LogicalName = rel.Entity1LogicalName,
                    Entity1IntersectAttribute = rel.Entity1IntersectAttribute,
                    Entity2LogicalName = rel.Entity2LogicalName,
                    Entity2IntersectAttribute = rel.Entity2IntersectAttribute,
                    IntersectEntityName = rel.IntersectEntityName,
                    SchemaName = rel.SchemaName
                }).ToList();

                relationshipList.AddRange(manyToManyRelationships);

                return new EntityRelationshipObj()
                {
                    entitySetName = entitySetName,
                    relationshipObj = relationshipList
                };
            }
            catch (Exception invalidEntityNameException)
            {
                InvalidPluginExecutionException invalidEntityNamePluginException =
                    new InvalidPluginExecutionException(
                        OperationStatus.Failed, "RetrieveRelationships on entity " + entityLogicalName + " failed with error message : " + invalidEntityNameException.Message);
                exceptionsQueue.Enqueue(invalidEntityNamePluginException);
                throw invalidEntityNamePluginException;
            }

        }
        // Retrieve Update Audit Records of recordsGUID(CSV) records
        private List<AuditObj> GetAuditsRecords(
            IOrganizationService service,
            string recordsGUID,
            int statusReasonColumnNumber,
            ITracingService tracingService)
        {
            Guid[] recordsGuidArray = ParseGuids(recordsGUID);

            QueryExpression auditQuery = AuditQueryProvider.GetAudits(statusReasonColumnNumber.ToString(), recordsGuidArray);

            EntityCollection auditCollection = service.RetrieveMultiple(auditQuery);


            List<AuditObj> auditList = auditCollection.Entities.Select(entity => new AuditObj()
            {
                changedAttributes = JsonSerializer.Deserialize<AuditObj>(entity["changedata"].ToString()).changedAttributes,
                createdon = entity["createdon"].ToString(),
                auditId = entity.Id,
                objectId = ((EntityReference)entity.Attributes["objectid"]).Id
            }

            ).ToList();

            return auditList;
        }
        // Get a collection of records status and status reason
        private List<ResponseRecordObj> GetRecordsState(
            IOrganizationService service,
            EntityCollection collection,
            string statusLabel,
            string statusReasonLabel,
            bool shouldRestorePreviousStatus,
            string primaryIdName,
            ConcurrentQueue<Exception> exceptionsQueue,
            ConcurrentBag<UpdatedRecordObj> updatedParentRecordsBag,
            ConcurrentBag<string> statusReasonNeverRestoredBag,
            string statusReasonNeverRestored,
            List<RelationshipObj> customEntitiesList,
            ITracingService tracingService,
            List<ResponseObj> responseObj)
        {
            string entityLogicalName = collection[0].LogicalName;
            try
            {
                bool isBpf = IsBusinessProcessFlow(service, entityLogicalName);
                return isBpf
                    ? ProcessBpfRecords(service, collection, entityLogicalName, primaryIdName, customEntitiesList, updatedParentRecordsBag, tracingService)
                    : ProcessStandardRecords(service, collection, entityLogicalName, statusLabel, statusReasonLabel, shouldRestorePreviousStatus, statusReasonNeverRestored, primaryIdName, updatedParentRecordsBag, statusReasonNeverRestoredBag, exceptionsQueue, tracingService);
            }
            catch (Exception ex)
            {
                var exception = new InvalidPluginExecutionException(OperationStatus.Failed,
                    $"Processing of entity {entityLogicalName} failed: {Environment.NewLine}{ex.Message}");
                exceptionsQueue.Enqueue(exception);
                throw exception;
            }
        }
        // Retrieves multiple records with "in" operator on a recordsGUID(CSV)
        private EntityCollection RetrieveMultipleRecords(
            IOrganizationService service,
            string entityLogicalName,
            string attributeLogicalName,
            string conditionAttributeLogicalName,
            string recordsGUID,
            ConcurrentQueue<Exception> exceptionsQueue,
            ITracingService tracingService)
        {

            if (string.IsNullOrWhiteSpace(recordsGUID))
                return new EntityCollection();
            try
            {

                Guid[] recordsGuidArray = ParseGuids(recordsGUID);

                QueryExpression multipleRecordsQuery = GenericInFilterQueryProvider.GetMultipleRecords(
                        entityLogicalName,
                        attributeLogicalName,
                        conditionAttributeLogicalName,
                        recordsGuidArray);

                return service.RetrieveMultiple(multipleRecordsQuery);
            }
            catch (Exception invalidRetrieveMultipleException)
            {
                InvalidPluginExecutionException invalidRetrieveMultiplePluginException =
                    new InvalidPluginExecutionException(
                        OperationStatus.Failed, "RetrieveMultple on entity " + entityLogicalName + " failed with error message : " + invalidRetrieveMultipleException.Message);
                exceptionsQueue.Enqueue(invalidRetrieveMultiplePluginException);
                throw invalidRetrieveMultiplePluginException;
            }


        }
        // Retrieves multiple records from N:N relationship with "in" operator on a recordsGUID(CSV)
        private EntityCollection RetrieveMultipleRecordsFromManyToMany(
            IOrganizationService service,
            string entityLogicalName,
            string attributeLogicalName,
            string intersectEntityName,
            string linkFromEntityAttribute,
            string linkToEntityAttribute,
            string recordsGUID,
            ConcurrentQueue<Exception> exceptionsQueue,
            ITracingService tracingService)
        {

            if (string.IsNullOrWhiteSpace(recordsGUID))
                return new EntityCollection();
            try
            {

                Guid[] recordsGuidArray = ParseGuids(recordsGUID);

                QueryExpression multipleRecordsQuery = GenericInFilterQueryProvider.GetMultipleRecordsFromManyToMany(
                        entityLogicalName,
                        attributeLogicalName,
                        intersectEntityName,
                        linkFromEntityAttribute,
                        linkFromEntityAttribute,
                        linkToEntityAttribute,
                        recordsGuidArray);

                return service.RetrieveMultiple(multipleRecordsQuery);
            }
            catch (Exception invalidRetrieveMultipleException)
            {
                InvalidPluginExecutionException invalidRetrieveMultiplePluginException =
                    new InvalidPluginExecutionException(
                        OperationStatus.Failed, "RetrieveMultple on entity " + entityLogicalName + " failed with error message : " + invalidRetrieveMultipleException.Message);
                exceptionsQueue.Enqueue(invalidRetrieveMultiplePluginException);
                throw invalidRetrieveMultiplePluginException;
            }


        }
        // Calculates Status value based on shouldRestorePreviousStatus feature flag
        private OptionSetValue CalculateStateCodeOptionSet(
            IOrganizationService service,
            List<AuditObj> auditList,
            Entity entity,
            bool shouldRestorePreviousStatus,
            int status,
            ConcurrentBag<string> statusReasonNeverRestoredBag,
            ITracingService tracingService)
        {
            List<AuditObj> currentEntityStateAuditList = new List<AuditObj>();
            AuditObj currentEntityLatestStateChange = new AuditObj();
            bool isLastAuditCurrentState = true;
            bool isLastAuditValid = true;

            if (shouldRestorePreviousStatus)
            {
                currentEntityStateAuditList = auditList.Where(audit =>
                Array.Exists(audit.changedAttributes, el => el.logicalName == "statuscode"
                & !statusReasonNeverRestoredBag.Contains(el.oldValue)
                & !statusReasonNeverRestoredBag.Contains(el.newValue)
                ) & audit.objectId == entity.Id).ToList();

                if (currentEntityStateAuditList.Any())
                {
                    currentEntityLatestStateChange = currentEntityStateAuditList[0];



                    isLastAuditCurrentState = IsLastStateAuditCurrentState(currentEntityLatestStateChange, entity, tracingService);
                    if (!isLastAuditCurrentState)
                    {
                        isLastAuditValid = IsLastStateAuditValid(currentEntityLatestStateChange, entity, tracingService);
                    }



                }
            }


            return

                shouldRestorePreviousStatus ?
                   currentEntityStateAuditList.Any() ?

                            Array.Exists(currentEntityLatestStateChange.changedAttributes, element => element.logicalName == "statecode") ?

                            isLastAuditCurrentState ?
                            Array.Find(currentEntityLatestStateChange.changedAttributes, element => element.logicalName == "statecode").oldValue == null ? new OptionSetValue(status) : new OptionSetValue(int.Parse(Array.Find(currentEntityLatestStateChange.changedAttributes, element => element.logicalName == "statecode").oldValue)) :
                            isLastAuditValid ?
                            Array.Find(currentEntityLatestStateChange.changedAttributes, element => element.logicalName == "statecode").newValue == null ? new OptionSetValue(status) : new OptionSetValue(int.Parse(Array.Find(currentEntityLatestStateChange.changedAttributes, element => element.logicalName == "statecode").newValue)) :
                            new OptionSetValue(status)
                             :

                            new OptionSetValue(((OptionSetValue)entity.Attributes["statecode"]).Value)
                 :
                new OptionSetValue(status) :
                new OptionSetValue(status);
        }
        // Calculates Status Reason value based on shouldRestorePreviousStatus feature flag
        private OptionSetValue CalculateStatusCodeOptionSet(
            IOrganizationService service,
            List<AuditObj> auditList,
            Entity entity,
            bool shouldRestorePreviousStatus,
            int statusReason,
            ConcurrentBag<string> statusReasonNeverRestoredBag,
            ITracingService tracingService)
        {
            List<AuditObj> currentEntityStateAuditList = new List<AuditObj>();
            AuditObj currentEntityLatestStateChange = new AuditObj();
            bool isLastAuditCurrentState = true;
            bool isLastAuditValid = true;

            if (shouldRestorePreviousStatus)
            {

                currentEntityStateAuditList = auditList.Where(audit => Array.Exists(audit.changedAttributes, el => el.logicalName == "statuscode"
                & !statusReasonNeverRestoredBag.Contains(el.oldValue)
                & !statusReasonNeverRestoredBag.Contains(el.newValue)
                )
                & audit.objectId == entity.Id).ToList();
                if (currentEntityStateAuditList.Any())
                {
                    currentEntityLatestStateChange = currentEntityStateAuditList[0];

                    isLastAuditCurrentState = IsLastStateAuditCurrentState(currentEntityLatestStateChange, entity, tracingService);
                    if (!isLastAuditCurrentState)
                    {
                        isLastAuditValid = IsLastStateAuditValid(currentEntityLatestStateChange, entity, tracingService);
                    }
                }
            }

            return
            shouldRestorePreviousStatus ?

            currentEntityStateAuditList.Any() ?
                    isLastAuditCurrentState ?
                    Array.Find(currentEntityLatestStateChange.changedAttributes, element => element.logicalName == "statuscode").oldValue == null ? new OptionSetValue(statusReason) : new OptionSetValue(int.Parse(Array.Find(currentEntityLatestStateChange.changedAttributes, element => element.logicalName == "statuscode").oldValue)) :
                    isLastAuditValid ?
                    Array.Find(currentEntityLatestStateChange.changedAttributes, element => element.logicalName == "statuscode").newValue == null ? new OptionSetValue(statusReason) : new OptionSetValue(int.Parse(Array.Find(currentEntityLatestStateChange.changedAttributes, element => element.logicalName == "statuscode").newValue)) :

                    new OptionSetValue(statusReason)
                     :
                    new OptionSetValue(statusReason) :
                    new OptionSetValue(statusReason);
        }
        // Retrieves OptionsSet integer value from "selectedText" and ColumnNumber for a specific entity
        private AttributeMetaDataObj GetAttributeMetadataFromText(
            IOrganizationService service,
            string entityName,
            string attributeName,
            string selectedText,
            ITracingService tracingService)
        {
            RetrieveAttributeRequest retrieveAttributeRequest = new
            RetrieveAttributeRequest
            {
                EntityLogicalName = entityName,
                LogicalName = attributeName,
                RetrieveAsIfPublished = true
            };

            RetrieveAttributeResponse retrieveAttributeResponse = (RetrieveAttributeResponse)service.Execute(retrieveAttributeRequest);

            EnumAttributeMetadata retrievedPicklistAttributeMetadata = (EnumAttributeMetadata)retrieveAttributeResponse.AttributeMetadata;

            OptionMetadata[] optionList = retrievedPicklistAttributeMetadata.OptionSet.Options.ToArray();

            int selectedOptionValue = -1;
            int selectedColumnNumber = (int)retrievedPicklistAttributeMetadata.ColumnNumber;

            foreach (OptionMetadata oMD in optionList)
            {

                if (oMD.Label.LocalizedLabels[0].Label.ToString() == selectedText)
                {
                    selectedOptionValue = oMD.Value != null ? (int)oMD.Value : -1;
                    break;
                }
            }

            return new AttributeMetaDataObj()
            {
                columnNumber = selectedColumnNumber,
                optionValue = selectedOptionValue
            };

        }
        // Retrieves Unique Identifier Name from "entityName"
        private EntityMetaDataObj GetEntityMetadata(
            IOrganizationService service,
            string entityName,
            ITracingService tracingService)
        {
            RetrieveEntityRequest retrieveAttributeRequest = new
            RetrieveEntityRequest
            {
                LogicalName = entityName,
                EntityFilters = EntityFilters.All,
                RetrieveAsIfPublished = true
            };

            RetrieveEntityResponse retrieveEntityResponse = (RetrieveEntityResponse)service.Execute(retrieveAttributeRequest);
            bool isBpf = retrieveEntityResponse.EntityMetadata.IsBusinessProcessEnabled ?? false;
            ParameterCollection parameterCollection = retrieveEntityResponse.Results;
            List<string> entityMetaDataList = parameterCollection.Values.Select(parameter =>
            (string)parameter.GetType().GetProperty("PrimaryIdAttribute").GetValue(parameter, null)
            ).ToList();

            string primaryEntityIdName = entityMetaDataList.First();

            return new EntityMetaDataObj()
            {
                primaryIdAttribute = primaryEntityIdName
            };
        }
        // Checks for string existence inside ConcurrentBag of string
        private bool IsStringInsideBag(
            ConcurrentBag<string> concurrentBag,
            string _string)
        {
            return concurrentBag.ToList().Exists(el => el == _string);
        }
        // Checks for a particular audit if it matches the current status or not
        // Audit can take some time to be created. 
        // This method ensure we can detect if the audit has not been created yet and act accordingly
        private bool IsLastStateAuditCurrentState(
            AuditObj entityLatestStateAudit,
            Entity entity,
            ITracingService tracingService)
        {
            string entityStatus = ((OptionSetValue)entity.Attributes["statecode"]).Value.ToString();
            string entityStatusReason = ((OptionSetValue)entity.Attributes["statuscode"]).Value.ToString();
            // status can be missing in audit if only status reason changes over the last update
            string auditStatus = Array.Exists(entityLatestStateAudit.changedAttributes, element => element.logicalName == "statecode") ?
                Array.Find(entityLatestStateAudit.changedAttributes, element => element.logicalName == "statecode").newValue : entityStatus;
            string auditStatusReason = Array.Find(entityLatestStateAudit.changedAttributes, element => element.logicalName == "statuscode").newValue;
            return entityStatus == auditStatus & entityStatusReason == auditStatusReason;

        }
        // Checks if a particular audit has a statusReason different from current entity one
        private bool IsLastStateAuditValid(
            AuditObj entityLatestStateAudit,
            Entity entity,
            ITracingService tracingService)
        {

            string entityStatusReason = ((OptionSetValue)entity.Attributes["statuscode"]).Value.ToString();

            string auditStatusReason = Array.Find(entityLatestStateAudit.changedAttributes, element => element.logicalName == "statuscode").newValue;
            return entityStatusReason != auditStatusReason;

        }
        private List<StageObj> GetFinalStages(
    JsonDocument clientdata)
        {
            JsonElement root = clientdata.RootElement;
            JsonElement stepArray = root.GetProperty("steps").GetProperty("list");

            return stepArray.EnumerateArray()
            .Where(el => el.GetProperty("steps").GetProperty("list").GetArrayLength() > 0)
            .Select(el => new StageObj()
            {
                id = el.GetProperty("steps").GetProperty("list")[0].TryGetProperty("stageId", out var idElement) && idElement.ValueKind == JsonValueKind.String
? idElement.GetString()
: "",
                name = el.GetProperty("steps").GetProperty("list")[0].TryGetProperty("description", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
? nameElement.GetString()
: "",
                nextStageId = el.GetProperty("steps").GetProperty("list")[0].TryGetProperty("nextStageId", out var stageElement) && stageElement.ValueKind == JsonValueKind.String
? nameElement.GetString()
: "",
                innerData = el.GetProperty("steps").GetProperty("list")[0].TryGetProperty("steps", out var innerSteps) && innerSteps.TryGetProperty("list", out var innerList)
                ? innerList.GetRawText() : ""

            })
            .Where(el => el.nextStageId == "" && !el.innerData.Contains("SetNextStageStep"))
            .ToList();
        }
        private void ProcessEntityLogicalNames(
            string entityLogicalNamesCsv,
            string actionDescription,
            string publisherPrefix,
            ConcurrentBag<string> targetBag,
            ConcurrentQueue<Exception> exceptionsQueue)
        {
            if (string.IsNullOrWhiteSpace(entityLogicalNamesCsv))
                return;

            var entityNames = entityLogicalNamesCsv
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim());

            string expectedPrefix = publisherPrefix + "_";

            foreach (var entityName in entityNames)
            {
                if (entityName.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) || actionDescription == "include")
                {
                    targetBag.Add(entityName);
                }
                else
                {
                    string errorMessage = $"EntityLogicalName to {actionDescription} '{entityName}' failed: Entity publisher prefix doesn't match with the one provided.";
                    InvalidPluginExecutionException exception = new InvalidPluginExecutionException(OperationStatus.Failed, errorMessage);
                    exceptionsQueue.Enqueue(exception);
                    throw exception;
                }
            }
        }
        private bool IsBusinessProcessFlow(IOrganizationService service, string logicalName)
        {
            var query = ProcessQueryProvider.GetWorkflowByEntityLogicalName(logicalName);
            var collection = service.RetrieveMultiple(query);
            return collection.Entities.Any();
        }

        private List<ResponseRecordObj> ProcessBpfRecords(
    IOrganizationService service,
    EntityCollection collection,
    string entityLogicalName,
    string primaryIdName,
    List<RelationshipObj> customEntitiesList,
    ConcurrentBag<UpdatedRecordObj> updatedParentRecordsBag,
    ITracingService tracingService)
        {
            string referencingAttribute = customEntitiesList.Find(rel => rel.ReferencingEntity == entityLogicalName).ReferencingAttribute;
            string referencedAttribute = customEntitiesList.Find(rel => rel.ReferencingEntity == entityLogicalName).ReferencedAttribute;
            string referencedEntity = customEntitiesList.Find(rel => rel.ReferencingEntity == entityLogicalName).ReferencedEntity;

            Guid[] bpfGuidArray = collection.Entities.Select(entity => entity.Id).ToArray();

            QueryExpression bpfQuery = BusinessProcessFlowQueryProvider.GetBpfRecords(
                entityLogicalName,
                primaryIdName,
                primaryIdName,
                bpfGuidArray,
                referencedEntity,
                referencedAttribute,
                referencingAttribute
                );


            EntityCollection bpfCollection = service.RetrieveMultiple(bpfQuery);
            EntityReference processReference = bpfCollection.Entities.FirstOrDefault().GetAttributeValue<EntityReference>("processid");
            QueryExpression processQuery = ProcessQueryProvider.GetWorkflowById(processReference.Id.ToString());
            EntityCollection processesCollection = service.RetrieveMultiple(processQuery);
            Entity process = processesCollection.Entities.FirstOrDefault();
            List<StageObj> finalStagesList = new List<StageObj>();
            JsonDocument clientdata = JsonDocument.Parse(process.GetAttributeValue<string>("clientdata"));
            finalStagesList = GetFinalStages(clientdata);


            bool isParentUpdated = updatedParentRecordsBag.Any(el => el.entityName == referencedEntity);


            List<ResponseRecordObj> updatedCollection = bpfCollection.Entities.Select(entity => new ResponseRecordObj()
            {
                recordId = entity.Id,
                entityLogicalName = entityLogicalName,
                statecode = isParentUpdated ?

                    updatedParentRecordsBag.Where(
                        el => el.entityName == referencedEntity &&
                        el.recordId == (Guid)entity.GetAttributeValue<AliasedValue>("parent." + referencedAttribute).Value).First().status.Value :

                        ((OptionSetValue)entity.GetAttributeValue<AliasedValue>("parent.statecode").Value).Value,
                statuscode = (
                        isParentUpdated ?

                        updatedParentRecordsBag.Where(
                            el => el.entityName == referencedEntity &&
                            el.recordId == (Guid)entity.GetAttributeValue<AliasedValue>("parent." + referencedAttribute).Value).First().status.Value :

                            ((OptionSetValue)entity.GetAttributeValue<AliasedValue>("parent.statecode").Value).Value) == 1 ?
                        finalStagesList.Exists(el => el.id == entity.GetAttributeValue<AliasedValue>("activestage.processstageid").Value.ToString()) ? 2 : 3 :
                        1
            }).ToList();

            return updatedCollection;
        }
        private List<ResponseRecordObj> ProcessStandardRecords(
    IOrganizationService service,
    EntityCollection collection,
    string entityLogicalName,
    string statusLabel,
    string statusReasonLabel,
    bool shouldRestorePreviousStatus,
    string statusReasonNeverRestored,
    string primaryIdName,
    ConcurrentBag<UpdatedRecordObj> updatedParentRecordsBag,
    ConcurrentBag<string> statusReasonNeverRestoredBag,
    ConcurrentQueue<Exception> exceptionsQueue,
    ITracingService tracingService)
        {
            int status = -1;
            int statusReason = -1;
            AttributeMetaDataObj statusMetadata = GetAttributeMetadataFromText(service, entityLogicalName, "statecode", statusLabel, tracingService);
            AttributeMetaDataObj statusReasonMetadata = GetAttributeMetadataFromText(service, entityLogicalName, "statuscode", statusReasonLabel, tracingService);
            status = statusMetadata.optionValue;
            statusReason = statusReasonMetadata.optionValue;

            if (status == -1 || statusReason == -1)
            {
                var exception = new InvalidPluginExecutionException(OperationStatus.Failed,
    "Invalid status or statusReason provided");
                exceptionsQueue.Enqueue(exception);
                throw exception;
            }



            List<AuditObj> auditList = new List<AuditObj>();
            if (shouldRestorePreviousStatus)
            {

                List<string> RecordsGUIDList = collection.Entities.Select(en => en.Id.ToString()).ToList();
                string RecordsGUID = string.Join(",", RecordsGUIDList);

                auditList = GetAuditsRecords(service, RecordsGUID, statusReasonMetadata.columnNumber, tracingService);


                foreach (var _statusReason in statusReasonNeverRestored.Split(',').Where(s => !string.IsNullOrWhiteSpace(s)))
                {
                    int _optionset = GetAttributeMetadataFromText(service, entityLogicalName, "statuscode", _statusReason, tracingService).optionValue;

                    if (_optionset != -1) statusReasonNeverRestoredBag.Add(_optionset.ToString());

                }
            }

            List<Entity> updatedCollection = collection.Entities.Select(entity => new Entity(entity.LogicalName)
            {
                Id = entity.Id,
                ["statecode"] = CalculateStateCodeOptionSet(
                    service, auditList, entity, shouldRestorePreviousStatus, status, statusReasonNeverRestoredBag, tracingService),
                ["statuscode"] = CalculateStatusCodeOptionSet(
                    service, auditList, entity, shouldRestorePreviousStatus, statusReason, statusReasonNeverRestoredBag, tracingService)
            }
            ).ToList();


            foreach (Entity entity in updatedCollection)
            {
                updatedParentRecordsBag.Add(new UpdatedRecordObj()
                {
                    entityName = entity.LogicalName,
                    recordId = entity.Id,
                    status = (OptionSetValue)entity["statecode"]
                });
            }



            List<ResponseRecordObj> updatedResponseCollection = updatedCollection.Select(entity => new ResponseRecordObj()
            {
                recordId = entity.Id,
                entityLogicalName = entityLogicalName,
                statecode = ((OptionSetValue)entity["statecode"]).Value,
                statuscode = ((OptionSetValue)entity["statuscode"]).Value
            }

 ).ToList();




            return updatedResponseCollection;




        }
        private Guid[] ParseGuids(string commaSeparatedGuids)
        {
            return commaSeparatedGuids
                .Split(',')
                .Where(s => Guid.TryParse(s, out _))
                .Select(Guid.Parse)
                .ToArray();
        }
        #endregion
    }
}