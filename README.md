# WingetUpdaterConsole

A Windows console application that provides an interactive interface for managing software updates via Windows Package Manager (winget).

## Features

- Lists available software updates from winget
- Interactive update selection with version comparison highlighting
- Ability to ignore specific packages using winget pin
- Detailed logging of update operations
- Support for major/minor version change indicators

## Requirements

- Windows OS
- .NET 10.0 Runtime
- Windows Package Manager (winget)

## Installation

1. Clone the repository
2. Build the solution:
   ```
   dotnet build Consoler/Consoler.sln
   ```

## Usage

Run the compiled executable:

```
Consoler.exe
```

### Command-line Options

- `--debug`: Enable loader debug mode
- `--dbg-loader-type <type>`: Set loader type (1 or 2)
- `--dbg-symbols <symbols>`: Custom loader animation symbols
- `--dbg-response-file <path>`: Use a response file for testing

## Project Structure

- `Consoler/Consoler/`: Main application
- `Consoler/FakeWinget/`: Test utility for simulating winget
- `Consoler/Consoler.Tests/`: Unit tests

## License

This is a personal project. See repository for license details.
