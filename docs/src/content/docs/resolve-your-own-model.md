---
title: "Resolve your own model"
description: "Implement the shape and the resolver that let templates read your own objects, and the rules the base resolver carries."
audience: "Developers"
kind: "How-to guide"
status: "Current"
---

> **Status: Current.** How-to guide for developers.

Two interfaces describe a model. `IShape` is the static view `Validate` walks: the members a value has, whether it is a collection, the shape of its items, and the variants of an abstract shape. `IResolver` is the runtime view the renderer reads through: `TryGetMember`, `Items`, `IsTrue`, `Format`, `Compare` and `Picture`. The `Resolver` base class carries the rules this document describes for iteration, truthiness, formatting and ordering, and takes a byte array as a picture; derive from it, implement `TryGetMember`, and override the rest for your own value types. A resolver may hand out its own value types and recognise them again when it formats, compares or iterates them, which is how a host prints an amount in its currency.

| Provider | Shape | Resolver | Members |
|---|---|---|---|
| Dictionary, in `Allors.Documents` | `DictionaryShape.Open`, `new DictionaryShape(members)` | `DictionaryResolver` | The keys of a dictionary, exactly as written, and nothing else. |
| Reflection, in `Allors.Documents.Reflection` | `ReflectionShape.Of<T>()`, `ReflectionShape.Arguments<T>()` | `ReflectionResolver` | Dictionary keys as written; on any other object the name with its first letter upper-cased or as written, as a property, a `Get` method, a `get_` method, a string indexer or a field. |

`TryGetMember` returns false for a member the scope does not have, which the renderer reports, and true with a null value for a member that is empty, which prints blank. The renderer never passes a null scope.
