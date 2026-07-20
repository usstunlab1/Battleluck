# Backtrace Error Reporting Setup

This document describes how to set up Backtrace error reporting for BattleLuck.

## Overview

Backtrace is a crash and error reporting service that captures:
- Unhandled exceptions
- Log errors (from `Debug.LogError`)
- Handled exceptions (explicitly reported)
- Game state context (mode, player count, etc.)

## Prerequisites

1. A Backtrace account (sign up at https://backtrace.io)
2. A Backtrace project with a submission token

## Configuration

Edit `BepInEx/config/BattleLuck/backtrace_config.json`:

```json
{
  "enabled": true,
  "serverAddress": "https://submit.backtrace.io",
  "submissionToken": "your-submission-token-here",
  "attributes": {
    "modVersion": "1.0.0",
    "game": "VRising"
  },
  "database": {
    "enabled": true,
    "path": "backtrace_database",
    "maxRecords": 100
  },
  "reporting": {
    "unhandledExceptions": true,
    "logErrors": true,
    "handledExceptions": true
  }
}
```

### Getting Your Server Address and Token

1. In the Backtrace Console, go to **Project Settings > Integration Guides > Unity**
2. Copy the server address in the format: `https://submit.backtrace.io/{subdomain}/{submission-token}/json`
3. Set `serverAddress` to `https://submit.backtrace.io`
4. Set `submissionToken` to your submission token

## Features

### Automatic Error Capture

The `UnityLogHandler` automatically captures:
- `Debug.LogError` messages
- Unity exceptions
- Unhandled exceptions

### Manual Error Reporting

You can report errors programmatically:

```csharp
// Report an exception with context
BattleLuckPlugin.ErrorReporter?.ReportException(
    exception,
    "Custom error message",
    new { PlayerId = steamId, Action = "SomeAction" }
);

// Report a log error
BattleLuckPlugin.ErrorReporter?.ReportLogError(
    "Error message",
    stackTrace,
    new { Context = "Additional info" }
);
```

### Dynamic Attributes

Update attributes at runtime:

```csharp
BattleLuckPlugin.ErrorReporter?.UpdateAttribute("gameMode", "bloodbath");
```

## Integration Points

The Backtrace service is integrated into:

1. **Plugin Initialization** - Initialized during `TryInitializeCore()`
2. **Log Handler** - Captures Unity log errors via `UnityLogHandler`
3. **Cleanup** - Properly disposed during plugin unload

## Notes

- For full Unity SDK features (crashes, hangs, out-of-memory on mobile), install the Backtrace Unity SDK via OpenUPM or Unity Package Manager
- This server-side implementation uses HTTP API for error reporting
- Error reports include game state context (active mode, player count, session state)