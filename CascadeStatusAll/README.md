# CascadeStatusAll

---

## Overview

This plugin implements **cascade retrieval of status and status reason** for a set of parent records and their related children in Microsoft Dataverse.  
It is intended to be used in combination with **Power Automate** for performing **bulk status updates** without making any changes directly.

The plugin supports advanced scenarios such as:

- Recursive cascade through N-level child relationships
- Audit-based restoration of previous status and status reason
- Entity inclusion/exclusion control
- Handling of complex parent-child relationships
- Intelligent recalculation of status for shared-child scenarios

---

## How It Works

Given a list of **parent record GUIDs** and input parameters, the Custom API:

1. Retrieves the **status** and **status reason** for parent records (if configured).
2. Recursively collects child relationships based on the `publisherPrefix` or inclusion list.
3. Collects the **current or previous states** of all related records, depending on flags.
4. Returns all records and their statuses in a form ready for further use (e.g. Power Automate).

> ⚠️ The plugin does **not perform any updates**. It only returns the required records and data.

---

## Input Parameters

| Parameter                           | Type    | Description |
|------------------------------------|---------|-------------|
| `recordsGUID`                      | CSV     | List of parent record GUIDs. |
| `shouldUpdateParent`              | Boolean | Whether parent records should be included in the output. |
| `shouldRestorePreviousStatus`     | Boolean | Whether to retrieve previous Status and Status Reason from Audit history. |
| `statusLabel`                     | String  | Default status label to use when not restoring from audit. |
| `statusReasonLabel`               | String  | Default status reason to use when not restoring from audit. |
| `publisherPrefix`                 | String  | Prefix to filter which entities' children to process. |
| `entitiesLogicalNamesToExclude`   | CSV     | Logical names of entities to exclude from the cascading process. |
| `entitiesLogicalNamesToInclude`   | CSV     | Entities to explicitly include even if they don’t match the prefix. |
| `entitiesLogicalNamesToRecalculate` | CSV   | Entities to always recalculate even if previously processed. |
| `shouldCascadeRecalculation`      | Boolean | If true, all children of recalculated entities will also be recalculated. |
| `statusReasonNeverRestored`       | CSV     | Status reasons to ignore when restoring from audit. |

---

## Example Scenarios

### 1. Deactivate a Parent and Its Entire Tree

You want to deactivate a parent record and all its children/subchildren with a custom status reason:

**Input:**

```json
{
  "recordsGUID": "parent-guid-1,parent-guid-2",
  "statusLabel": "Inactive",
  "statusReasonLabel": "Parent Interrupted",
  "shouldRestorePreviousStatus": false,
  "shouldUpdateParent": true
}
```
---

## Prerequisites

### General Requirements
- Set All Children Status Reason Label the same for all Children Entities except BPF entities, the value doesn't matter
- The Parent NEEDS to have the same Status Reason Values Labels of Children except BPF entities, but can use less

### `shouldRestorePreviousStatus` Requirements
- Enable Auditing on Environment and wait 12 hours for audit data migration to MongoDB-NOSQL
- Enable Auditing on all custom entities you plan to use for your project except BPF entities
- Enable Auditing on `statecode` and `statuscode` columns of those entities

---

## Limitations

- Standard 5000 items limitation for Dataverse queries applies
- If the Audits are cleared, `shouldRestorePreviousStatus` feature flag won't work and `statusLabel`,`statusReasonLabel` will be applied;
consider setting a data retention policy with the same retention period as the audit one or disable the action altogheter if a certain amount of time has passed since the last change of the record.
This prevent the situation of trying to restore a record that has no audit, atleast in the case of automatic audit deletion, since it will be retained and read only
- The API has several failsafes against spamming:
	1. If the API finds the last audit not to match the current state it assumes the audit to be the one before the last so it tries to restore by using the `newValue` instead of `oldValue`.
	2. The API checks for the audit to be written to be valid by ensuring that the values it is going to write are different from the current record values.
	3. If both return false, it will restore the values passed as inputs as fallback.

- Feature A
  1. Option 1
  2. Option 2