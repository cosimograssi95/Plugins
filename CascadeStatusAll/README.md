# CascadeStatusAll

---

## Overview

This Plugin implements **cascade retrieval of status and status reason** for a set of parent records and their related children in Microsoft Dataverse.  
It is intended to be used in combination with **Power Automate** for performing **bulk status updates** via **bulk messages** or **batch API**.

The plugin supports advanced scenarios such as:

- Recursive cascade through N-level child relationships of any type.
- Audit-based restoration of previous status and status reason.
- Entity inclusion/exclusion control.
- Handling of complex parent-child relationships.
- Intelligent recalculation of status for shared-child scenarios.

---

## How It Works

Given a list of **parent record GUIDs** and input parameters, the Plugin:

1. Retrieves the **status** and **status reason** for parent records.
2. Recursively collects child relationships based on the `publisherPrefix` or `entitiesLogicalNamesToInclude`.
3. Collects the **current or previous states** of all related records, depending on `shouldRestorePreviousStatus`.
4. Returns all records and their statuses in a expando object, ready for further use.

> ⚠️ The plugin does **not perform any updates**. It only returns the required records and data.

---

## Input Parameters

| Parameter                           | Type    | Description |
|------------------------------------|---------|-------------|
| `recordsGUID`                      | String(CSV) | List of parent record GUIDs. |
| `shouldUpdateParent`              | Boolean | Whether parent records should be included in the output. |
| `shouldRestorePreviousStatus`     | Boolean | Whether to retrieve previous Status and Status Reason from Audit history. |
| `statusLabel`                     | String  | Default status label to use when not restoring from audit. |
| `statusReasonLabel`               | String  | Default status reason to use when not restoring from audit. |
| `publisherPrefix`                 | String  | Prefix to filter which entities' children to process. |
| `entitiesLogicalNamesToExclude`   | String(CSV) | Logical names of entities to exclude from the cascading process. |
| `entitiesLogicalNamesToInclude`   | String(CSV) | Entities to explicitly include even if they don’t match the prefix. |
| `entitiesLogicalNamesToRecalculate` | String(CSV) | Entities to always recalculate even if previously processed. |
| `shouldCascadeRecalculation`      | Boolean | If true, all children of recalculated entities will also be recalculated. |
| `statusReasonNeverRestored`       | String(CSV) | Status reasons to ignore when restoring from audit. |

---

## Example Scenarios

### 1. Deactivate a Parent and Its Entire Tree

You want to deactivate a parent record and all its children/subchildren with a custom status reason.
`statusLabel`,`statusReasonLabel` will be used for every entity.

**Input:**

```json
{
  "recordsGUID": "parent-guid-1,parent-guid-2",
  "statusLabel": "Inactive",
  "statusReasonLabel": "Parent Interrupted",
  "shouldRestorePreviousStatus": false,
  "shouldUpdateParent": true,
  "entitiesLogicalNamesToExclude": null,
  "entitiesLogicalNamesToInclude": null,
  "entitiesLogicalNamesToRecalculate": null,
  "shouldCascadeRecalculation": false,
  "statusReasonNeverRestored": null
}
```

### 2. Restore Parent and Its Entire Tree

You want to restore a parent record and all its children/subchildren state.
`statusLabel`,`statusReasonLabel` will be used as fallback.

**Input:**

```json
{
  "recordsGUID": "parent-guid-1,parent-guid-2",
  "statusLabel": "Inactive",
  "statusReasonLabel": "Parent Interrupted",
  "shouldRestorePreviousStatus": true,
  "shouldUpdateParent": true,
  "entitiesLogicalNamesToExclude": null,
  "entitiesLogicalNamesToInclude": null,
  "entitiesLogicalNamesToRecalculate": null,
  "shouldCascadeRecalculation": false,
  "statusReasonNeverRestored": null
}
```

### 2. Restore Parent and Its Entire Tree when running the Plugin multiple times over the same entity

You want to restore a record and all its children/subchildren with a custom status reason.

This record went through this kind of changes:
- From 
```yaml
Status:"Active",
StatusReason:"Active"
```
to
```yaml
Status:"Inactive",
StatusReason:"Self Reason"
```
- Status="Active",StatusReason="Active" to Status="Inactive",StatusReason="Self Reason"
- From Status="Inactive",StatusReason="Self Reason" to Status="Inactive",StatusReason="Parent Reason"

This can happen if you run the plugin over the Child first, and later over the Parent, overriding the Status Reason.

In this scenario you want to:
- restore the Parent first, making the child go From Status="Inactive",StatusReason="Parent Reason" to Status="Inactive",StatusReason="Self Reason"

Now you would like to restore the previous state of the Child. Since the last state was Status="Inactive",StatusReason="Parent Reason", simply setting `shouldRestorePreviousStatus` won't work.

You have to specify `statusReasonNeverRestored` to be "Parent Reason". This way you skip over Status="Inactive",StatusReason="Parent Reason" and restore to Status="Active",StatusReason="Active".

**Input:**

```json
{
  "recordsGUID": "parent-guid-1,parent-guid-2",
  "statusLabel": "Inactive",
  "statusReasonLabel": "Parent Interrupted",
  "shouldRestorePreviousStatus": true,
  "shouldUpdateParent": false,
  "entitiesLogicalNamesToExclude": null,
  "entitiesLogicalNamesToInclude": null,
  "entitiesLogicalNamesToRecalculate": null,
  "shouldCascadeRecalculation": false,
  "statusReasonNeverRestored": "Parent Reason"
}
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
- The Plugin has several failsafes against spamming:
	- If the Plugin finds the last audit record not to match the current state, it assumes the audit record to be the second last so it tries to restore the correct value using the `newValue` instead of `oldValue`.
	- The Plugin checks the validity of the audit record by ensuring that the values it is going to write are different from the current record values.
	- If it cannot find a valid audit record, it will restore `statusLabel`,`statusReasonLabel` as fallback.