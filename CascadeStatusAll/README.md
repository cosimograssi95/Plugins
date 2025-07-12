# CascadeStatusAll

## Overview

This Plugin implements **cascade retrieval of status and status reason** for a set of parent records and their related children in Microsoft Dataverse.  
It is intended to be used in combination with **Power Automate**,**Client Sdk** by performing **bulk status updates** via **bulk messages** or **batch API**.

The plugin supports advanced scenarios such as:

- Recursive cascade through N-level child relationships of any type.
- Audit-based restoration of previous status and status reason.
- Entity inclusion/exclusion control.
- Handling of complex parent-child relationships.
- Intelligent recalculation of status for shared-child scenarios.

> ⚠️ The plugin implements a **recursive** algorithm. Depending on the amount of records, you might need to chunk the starting dataset in order to avoid **excessive execution time**.

---

## How It Works

Given a list of **parent record GUIDs** and input parameters, the Plugin:

1. Retrieves the **status** and **status reason** for parent records.
2. Recursively collects child relationships based on the `publisherPrefix` or `entitiesLogicalNamesToInclude`.
3. Collects the **current or previous states** of all related records, depending on `shouldRestorePreviousStatus`.
4. Returns all records and their statuses in a expando object, ready for further use.
---

## How to create the Custom API

### Clone the repository

```bash
git clone https://github.com/cosimograssi95/Plugins.git
```
Build the solution.

### Plugin Registration Tool

If you have PAC CLI installed you can download the tool with

```bash
pac tool prt
```

- Connect to your organization
- `Register > Register new package`.
- `Register > Register new Custom API` with the specified input and output parameters

> ⚠️ The plugin does **not perform any updates**. It only returns the required records and data.

---

## Input Parameters

| Parameter                           | Type    | Description | Required |
|------------------------------------|---------|-------------|-----------|
| `recordsGUID`                      | String(CSV) | List of parent record GUIDs. | true |
| `shouldUpdateParent`              | Boolean | Whether parent records should be included in the output. | true |
| `shouldRestorePreviousStatus`     | Boolean | Whether to retrieve previous Status and Status Reason from Audit history. | true |
| `statusLabel`                     | String  | Default status label to use when not restoring from audit. | true |
| `statusReasonLabel`               | String  | Default status reason to use when not restoring from audit. | true |
| `publisherPrefix`                 | String  | Prefix to filter which entities to process. | true |
| `entitiesLogicalNamesToExclude`   | String(CSV) | Logical names of entities to exclude from the cascading process. | false | 
| `entitiesLogicalNamesToInclude`   | String(CSV) | Entities to explicitly include even if they don’t match the prefix. | false |
| `entitiesLogicalNamesToRecalculate` | String(CSV) | Entities to always recalculate even if previously processed. | false |
| `shouldCascadeRecalculation`      | Boolean | If true, all children of recalculated entities will also be recalculated. | true |
| `statusReasonNeverRestored`       | String(CSV) | Status reasons to ignore when restoring from audit. | false |

---
## Output Parameters

| Parameter                           | Type    | Description |
|------------------------------------|---------|-------------|
| `response`                      | EntityCollection | List of response expando objects. |

`response` schema:

```json
{
  "title": "response",
  "type": "array",
  "items": {
    "type": "object",
    "properties": {
      "recordId": {
        "type": "string",
        "description": "The record identifier"
      },
      "statecode": {
        "type": "string",
        "description": "The state code"
      },
      "statuscode": {
        "type": "string",
        "description": "The status code"
      },
      "entityLogicalName": {
        "type": "string",
        "description": "The logical name of the entity"
      },
      "entitySetName": {
        "type": "string",
        "description": "The name of the entity set"
      }
    },
    "required": [
      "recordId",
      "statecode",
      "statuscode",
      "entityLogicalName",
      "entitySetName"
    ]
  }
}
```
---
## Example Scenarios

### 1. Deactivate a Parent and Its Entire Tree

You want to deactivate a parent record and all its children/subchildren with a custom status reason.
`statusLabel`,`statusReasonLabel` will be used for every entity record.

**Input:**

```yaml
  "recordsGUID": "parent-guid-1,parent-guid-2",
  "statusLabel": "Inactive",
  "statusReasonLabel": "Parent Interrupted",
  "publisherPrefix": "new",
  "shouldRestorePreviousStatus": false,
  "shouldUpdateParent": true,
  "entitiesLogicalNamesToExclude": null,
  "entitiesLogicalNamesToInclude": null,
  "entitiesLogicalNamesToRecalculate": null,
  "shouldCascadeRecalculation": false,
  "statusReasonNeverRestored": null
```

### 2. Restore Parent and Its Entire Tree

You want to restore a parent record and all its children/subchildren state.
`statusLabel`,`statusReasonLabel` will be used as fallback.

**Input:**

```yaml
  "recordsGUID": "parent-guid-1,parent-guid-2",
  "statusLabel": "Inactive",
  "statusReasonLabel": "Parent Interrupted",
  "publisherPrefix": "new",
  "shouldRestorePreviousStatus": true,
  "shouldUpdateParent": true,
  "entitiesLogicalNamesToExclude": null,
  "entitiesLogicalNamesToInclude": null,
  "entitiesLogicalNamesToRecalculate": null,
  "shouldCascadeRecalculation": false,
  "statusReasonNeverRestored": null
```
### 3. Deactivate a Parent and Its Entire Tree when using standard entities

You want to deactivate a parent record and all its children/subchildren state, including Standard Entities in the process.

**Input:**

```yaml
  "recordsGUID": "parent-guid-1,parent-guid-2",
  "statusLabel": "Inactive",
  "statusReasonLabel": "Parent Interrupted",
  "publisherPrefix": "new",
  "shouldRestorePreviousStatus": false,
  "shouldUpdateParent": true,
  "entitiesLogicalNamesToExclude": null,
  "entitiesLogicalNamesToInclude": "account,contact,task",
  "entitiesLogicalNamesToRecalculate": null,
  "shouldCascadeRecalculation": false,
  "statusReasonNeverRestored": null
```
### 4. Deactivate Parent and Its Entire Tree, stopping at a certain entity

You want to deactivate a parent record and all its children/subchildren state, stopping at a certain entity.

`entitiesLogicalNamesToExclude` takes precedence over `entitiesLogicalNamesToInclude`.

If children of entities listed in `entitiesLogicalNamesToExclude` are also children of another entity included in the process, those will be processed normally.

**Input:**

```yaml
  "recordsGUID": "parent-guid-1,parent-guid-2",
  "statusLabel": "Inactive",
  "statusReasonLabel": "Parent Interrupted",
  "publisherPrefix": "new",
  "shouldRestorePreviousStatus": true,
  "shouldUpdateParent": true,
  "entitiesLogicalNamesToExclude": "new_myentity,account",
  "entitiesLogicalNamesToInclude": "account",
  "entitiesLogicalNamesToRecalculate": null,
  "shouldCascadeRecalculation": false,
  "statusReasonNeverRestored": null
```
### 5. Deactivate Parent and Its Entire Tree, with entities with multiple parents

You want to deactivate a parent record and all its children/subchildren state, but one of the Child entities have multiple parents.

Consider this configuration:
- **EntityParent**
- **EntityChildA** (Parent EntityParent)
- **EntityChildB** (Parent EntityParent)
- **EntityFinalChildC** (Parent EntityChildA,EntityChildB)
- ...

If you do not include `entitiesLogicalNamesToExclude`, some of the **EntityFinalChildC** records won't be involved in the process since each entity is processed once.

`shouldCascadeRecalculation` controls this behaviour for Child entities of any Entity listed in `entitiesLogicalNamesToExclude`.

By setting

```yaml
  "shouldCascadeRecalculation": true
  "entitiesLogicalNamesToRecalculate": "EntityFinalChildC",
```

we ensure that **EntityFinalChildC** will always be recalculated, and `shouldCascadeRecalculation` ensure its children will be recalculated aswell.

**Input:**

```yaml
  "recordsGUID": "parent-guid-1,parent-guid-2",
  "statusLabel": "Inactive",
  "statusReasonLabel": "Parent Interrupted",
  "publisherPrefix": "new",
  "shouldRestorePreviousStatus": false,
  "shouldUpdateParent": true,
  "entitiesLogicalNamesToExclude": null,
  "entitiesLogicalNamesToInclude": null,
  "entitiesLogicalNamesToRecalculate": "EntityFinalChildC",
  "shouldCascadeRecalculation": true,
  "statusReasonNeverRestored": null
```

### 6. Restore Parent and Its Entire Tree when running the Plugin multiple times over the same entity

You want to restore a record and all its children/subchildren state.

Consider this configuration:
- **EntityParent**
- **EntityChild**

**EntityChild** record went through this set of changes:
- From 
```yaml
"status":"Active",
"statusReason":"Active"
```
to
```yaml
"status":"Inactive",
"statusReason":"Self Reason"
```
- From 
```yaml
"status":"Inactive",
"statusReason":"Self Reason"
```
to
```yaml
"status":"Inactive",
"statusReason":"Parent Reason"
```

This can happen if you run the plugin over **EntityChild** record, and later over the **EntityParent** record, overriding the Status Reason with **Parent Reason**.

In this scenario you want to restore both **EntityParent** record and **EntityChild** record:
- restore **EntityParent** record first, using `shouldRestorePreviousStatus`. **EntityChild** record goes
From 
```yaml
"status":"Inactive",
"statusReason":"Parent Reason"
```
to
```yaml
"status":"Inactive",
"statusReason":"Self Reason"
```

Now you would like to restore the previous state of **EntityChild** record. Since the last state was

```yaml
"status":"Inactive",
"statusReason":"Parent Reason"
```
simply setting `shouldRestorePreviousStatus` won't work.

You have to specify `statusReasonNeverRestored` to be **Parent Reason**. 
This way you skip over
```yaml
"status":"Inactive",
"statusReason":"Parent Reason"
```
and restore
```yaml
"status":"Active",
"statusReason":"Active"
```

**Input:**

```yaml
  "recordsGUID": "parent-guid-1,parent-guid-2",
  "statusLabel": "Inactive",
  "statusReasonLabel": "Parent Interrupted",
  "publisherPrefix": "new",
  "shouldRestorePreviousStatus": true,
  "shouldUpdateParent": false,
  "entitiesLogicalNamesToExclude": null,
  "entitiesLogicalNamesToInclude": null,
  "entitiesLogicalNamesToRecalculate": null,
  "shouldCascadeRecalculation": false,
  "statusReasonNeverRestored": "Parent Reason"
```
---

## Prerequisites

### General Requirements
- All entities involved in the process, outside of BPF entities, **need** to share atleast `statusLabel`,`statusReasonLabel`, the value doesn't matter.

### `shouldRestorePreviousStatus` Requirements
- Enable Auditing on Environment and wait 12 hours for audit data migration to MongoDB-NOSQL.
- Enable Auditing on all custom entities you plan to use for your project except BPF entities.
- Enable Auditing on `statecode`,`statuscode` of those entities.

---

## Limitations

- Standard 5000 items limitation for Dataverse queries applies
- If the Audits are cleared, `shouldRestorePreviousStatus` feature flag won't work and `statusLabel`,`statusReasonLabel` will be applied;
consider setting a data retention policy with the same retention period as the audit one or disable the action altogheter if a certain amount of time has passed since the last change of the record.
This prevent the situation of trying to restore a record that has no audit, atleast in the case of automatic audit deletion, since it will be retained and read only
- The Plugin has several failsafes:
	- If the Plugin finds the last audit record not to match the current state, it assumes the audit record to be the second last so it tries to restore the correct value using the `newValue` instead of `oldValue`.
	- The Plugin checks the validity of the audit record by ensuring that the values it is going to write are different from the current record values.
	- If it cannot find a valid audit record, it will restore `statusLabel`,`statusReasonLabel` as fallback.