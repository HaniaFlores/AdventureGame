# Overview

Nightfall: An Adventure Game

Nightfall is a text adventure where the player navigates scenes, makes choices, manages health and inventory, and can save/load progress. The scene system is data-driven via a dictionary. A base SceneBase class provides shared behavior, while DialogueScene and CombatScene demonstrate inheritance and polymorphism. Player attributes are captured in a Stats struct, and a small union-style struct (DamageUnion) shows explicit memory layout. The program persists state to save.json using System.Text.Json.

I created this software to practice real-world fundamentals: variables, expressions, conditionals, loops, functions, classes, inheritance, structures, file I/O, and a union-like construct in C#.

[Software Demo Video](http://youtube.link.goes.here)

# Development Environment

- .NET 9.0.306
- C#
- Visual Studio Code

# Useful Websites

* [C# language reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/)
* [Records (C# reference)](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record)
* [Structure types (C# reference)](http://url.link.goes.here)
* [Structure types](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/struct)
* [JSON serialization and deserialization in .NET](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/overview)
* [File and Stream I/O](https://learn.microsoft.com/en-us/dotnet/standard/io/)
* [Tutorial: Create a .NET console application using Visual Studio Code](https://learn.microsoft.com/en-us/dotnet/core/tutorials/with-visual-studio-code)