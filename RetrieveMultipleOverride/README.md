# RetrieveMultipleOverride

## Overview

This Plugin implements **an override for RetrieveMultiple Message** that allows you to query similar records freely within a model driven subgrid.
This code is meant to serve as a template, feel free to implement your own `customLogic` and `customFilter`. 


> ⚠️ https://learn.microsoft.com/en-us/power-apps/developer/data-platform/best-practices/business-logic/limit-registration-plugins-retrieve-retrievemultiple.

---

## How It Works

<img width="1672" height="412" alt="image" src="https://github.com/user-attachments/assets/d597e0f3-dcdf-40e9-9b73-aa231ceb6e3f" />

<img width="1701" height="574" alt="image" src="https://github.com/user-attachments/assets/6cf55931-8201-4bef-be3c-88283656b352" />

Given an entity, the Plugin:

1. Intercept the RetrieveMultiple message.
2. Detect if it is called within a subgrid and retrieves the parent entity guid of the form.
3. Replace the link entity with your custom filter.

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

Create a new Step on your entity

| Parameter                           | Value    |
|------------------------------------|---------|
| `Message`                      | RetrieveMultiple |
| `Primary Entity`              | {YourEntity} |
| `Stage`     | PreOperation |

---

## Example Scenarios

### 1. Show similar records within the same form

You want to show Cases with the same keyword as your currently open Case, or maybe you would like to show all Expenses that happened during the same month as the current open Expense.

---

## Prerequisites

### General Requirements
- The entity you plan to register this plugin on requires a **Self Referential** relationship, implemented by creating a LookUp column on itself. This column data does not need to be filled nor maintained.

