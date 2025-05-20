# Contributing to ClearGlass

Thank you for your interest in contributing to ClearGlass! This document provides guidelines and instructions for contributing to the project.

## Code of Conduct

By participating in this project, you agree to maintain a respectful and inclusive environment for everyone.

## How to Contribute

### Reporting Bugs

1. Check if the bug has already been reported in the Issues section
2. If not, create a new issue with:
   - A clear, descriptive title
   - Steps to reproduce the issue
   - Expected behavior
   - Actual behavior
   - Screenshots if applicable
   - System information (Windows version, .NET version)

### Suggesting Features

1. Check if the feature has already been suggested
2. Create a new issue with:
   - A clear, descriptive title
   - Detailed description of the feature
   - Use cases and benefits
   - Any implementation ideas you have

### Pull Requests

1. Fork the repository
2. Create a new branch for your feature/fix
3. Make your changes
4. Ensure your code follows the project's coding standards
5. Add or update tests if necessary
6. Update documentation if needed
7. Submit a pull request

## Development Setup

1. Install Visual Studio 2022 with:

   - .NET Desktop Development workload
   - Windows SDK
   - C# development tools

2. Clone the repository:

   ```bash
   git clone https://github.com/yourusername/ClearGlass.git
   cd ClearGlass
   ```

3. Build the solution in Visual Studio

## Coding Standards

- Follow C# coding conventions
- Use meaningful variable and method names
- Add XML documentation for public methods
- Keep methods focused and small
- Use async/await for I/O operations
- Handle exceptions appropriately
- Write unit tests for new features

## Commit Messages

- Use clear, descriptive commit messages
- Reference issue numbers when applicable
- Use present tense ("Add feature" not "Added feature")
- Keep messages concise but informative

## Testing

- Test your changes thoroughly
- Ensure all existing tests pass
- Add new tests for new features
- Test on different Windows versions if possible

## Documentation

- Update README.md if necessary
- Add XML documentation for new public APIs
- Update comments for complex code sections
- Document any new configuration options

## Review Process

1. All pull requests will be reviewed
2. Address any feedback from reviewers
3. Ensure CI checks pass
4. Wait for approval from maintainers

## Questions?

Feel free to open an issue for any questions about contributing to the project.
