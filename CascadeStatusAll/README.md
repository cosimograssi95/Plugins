# CascadeStatusAll

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