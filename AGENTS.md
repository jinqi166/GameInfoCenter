# GameInfoCenter - 游戏资源采集服务

## Project Overview

GameInfoCenter is a Game Resource Collection Service (游戏资源采集服务).

## Cursor Cloud specific instructions

### Repository State

This repository is freshly initialized with only a README.md. No source code, dependencies, or build configuration exists yet.

### Technology Context (from user rules)

- Target platform: Unity 2022.3.61
- Language: C#
- Coding conventions:
  - Prefer `struct` over `class` for new data structures; pass structs by `ref`
  - Use `StringBuilder` or `ZString` for string concatenation (no `$` interpolation or `+`)
  - Use `UnityEngine.Pool` or `System.Buffers.ArrayPool` for collections (avoid frequent `new`)
  - Do not use `<see cref>` in comments
  - Enterprise-grade algorithms and implementations required

### Development Environment

Since this is an empty repository, there is no build/test/lint pipeline configured yet. Future agents should:

1. Check if source code has been added before attempting to build or test.
2. If Unity project structure exists, note that Unity Editor CLI (`Unity -batchmode`) is not available in Cloud Agent VMs.
3. For C# code that can be tested outside Unity (e.g., pure logic, services), use `dotnet` CLI for building and testing.

### Running Services

No services are defined. When services are added, document startup instructions here.
