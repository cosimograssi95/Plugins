# RetrieveMultipleOverride

## Overview

This plugin implements **an override for the RetrieveMultiple message**, allowing you to query related or similar records dynamically within a model-driven subgrid.
It serves as a flexible template, feel free to customize the `customLogic` and `customFilter` regions to fit your specific use case.


> ⚠️ Plugin registration on the RetrieveMultiple message should be done cautiously. For best practices, refer to the official documentation [here](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/best-practices/business-logic/limit-registration-plugins-retrieve-retrievemultiple).

---

## How It Works

<img width="1672" height="412" alt="image" src="https://github.com/user-attachments/assets/d597e0f3-dcdf-40e9-9b73-aa231ceb6e3f" />

<img width="1701" height="574" alt="image" src="https://github.com/user-attachments/assets/6cf55931-8201-4bef-be3c-88283656b352" />

When a RetrieveMultiple message is triggered on an entity, this Plugin:

1. Intercepts the RetrieveMultiple request.
2. Detects if the request originates from within a subgrid and retrieves the parent entity’s GUID from the form context.
3. Replaces the default link-entity filter with your custom filter logic to return the desired related or similar records.

---

## How to create and use the Plugin

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

### Use the Plugin

Register a new step on the target entity with these settings:

| Parameter                           | Value    |
|------------------------------------|---------|
| `Message`                      | RetrieveMultiple |
| `Primary Entity`              | `EntityLogicalName` |
| `Stage`     | PreOperation |

Add a Subgrid to your Entity Form and make sure `Show Related Records` is active

---

## Example Scenarios

### 1. Show similar records within the same form

- Display all Cases sharing the same keyword as the currently open Case.
- Show Expenses occurring within the same month as the current Expense record.

This enables contextual, dynamic filtering in subgrids to improve data relevance.

---

## Prerequisites

### General Requirements
- The target entity must have a **self-referential relationship**: a Lookup column pointing back to the same entity.
- The Lookup column does not require data population but must exist for the plugin logic to hook into.

