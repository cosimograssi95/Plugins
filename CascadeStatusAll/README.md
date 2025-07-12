# CascadeStatusAll

## Overview

This Plugin implements **cascade retrieval of status and status reason** for a set of records and their related children in Microsoft Dataverse.
It is intended to be used in combination with **Power Automate** for performing bulk status updates **without making any updates directly**.

The API supports advanced scenarios including:
- Cascading through N-level child relationships
- Restoring previous statuses from Audit history
- Excluding or including specific entities
- Handling complex parent-child dependency graphs

---

## How It Works
---

Given a set of **parent record GUIDs** and configuration flags, the API:
1. Retrieves the current **status** and **status reason** of those parent records (if enabled).
2. Traverses all child relationships recursively (filtered by publisher prefix or inclusion list).
3. Returns all records required to perform a massive update, including their current state.
4. Optionally **restores previous status/state** using Audit history.

No updates are performed directly the results can be consumed and processed further in Power Automate.

---

## Input Parameters

| Parameter                          | Type    | Description |
|------------------------------------|---------|-------------|
| `recordsGUID`                      | CSV     | List of parent record GUIDs |
| `shouldUpdateParent`              | Boolean | Whether to include parent records in output |
| `shouldRestorePreviousStatus`     | Boolean | Whether to attempt status restoration via Audit logs |
| `statusLabel`                     | String  | Fallback status label to use if not restoring from audit |
| `statusReasonLabel`               | String  | Fallback status reason to use if not restoring from audit |
| `publisherPrefix`                 | String  | Prefix used to filter custom entities |
| `entitiesLogicalNamesToExclude`   | CSV     | Entities to exclude from cascading |
| `entitiesLogicalNamesToInclude`   | CSV     | Entities to include even if they dont match the prefix |
| `entitiesLogicalNamesToRecalculate` | CSV   | Entities that should be recalculated even if already processed |
| `shouldCascadeRecalculation`      | Boolean | Whether to force recalculation of child entities of recalculated entities |
| `statusReasonNeverRestored`       | CSV     | Status reasons that should be excluded from restoration logic |

---

## ?? Scenario Example

### Deactivate a Parent and Children:
You want to deactivate a parent and its children with a custom reason: