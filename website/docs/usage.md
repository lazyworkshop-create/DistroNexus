---
sidebar_position: 3
---

# User Guide

DistroNexus provides a native WPF desktop workflow backed by a PowerShell module command surface.

## Core Workflows

### Install and Bootstrap

Use DistroNexus to install new WSL instances to custom locations and apply initial setup options.

Typical flow:
1.  Select distribution family and version.
2.  Choose install location.
3.  Configure initial credentials if required.
4.  Start installation and monitor progress.

### Instance Lifecycle Management

Manage registered WSL instances through lifecycle operations:

*   Start
*   Stop
*   Open terminal
*   Move
*   Rename
*   Remove
*   Set or reset default credentials

### Package and Catalog Operations

Use package and catalog actions to maintain offline assets and metadata:

*   Query available package definitions
*   Save/remove cached packages
*   Refresh catalog metadata

### Template-Assisted Automation

Built-in templates help bootstrap development environments consistently.

*   Discover templates through the template list.
*   Apply a template to execute a guided setup.
*   Use template automation commands for repeatable provisioning.

## Common Example: Move an Instance to Another Drive

1.  Select the target instance.
2.  Choose **Move** and set destination path.
3.  Confirm operation and wait for completion.
4.  Verify the instance appears with the updated location/state.
