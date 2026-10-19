---
title: "Resolve your own model"
description: "Implement the shape and the resolver that let templates read your own objects, and the rules the base resolver carries."
audience: "Developers"
kind: "How-to guide"
status: "Current"
---

> **Status: Current.** How-to guide for developers.

This guide is for a host whose objects are neither plain classes nor dictionaries, such as an
object model with metadata of its own. You give the library two views of your model: a shape for
`Validate` and a resolver for `Render`.

Two interfaces describe a model. `IShape` is the static view `Validate` walks: the kind of value, the members it has, the shape of its items when it is a collection, and the variants of an abstract shape. `IResolver` is the runtime view the renderer reads through: `TryGetMember`, `Items`, `IsTrue`, `Format`, `Compare` and `Picture`. The `Resolver` base class carries the rules this document describes for iteration, truthiness, formatting and ordering, and takes a byte array as a picture; derive from it, implement `TryGetMember`, and override the rest for your own value types. A resolver may hand out its own value types and recognize them again when it formats, compares or iterates them, which is how a host prints an amount in its currency.

`Kind` is a `ValueKind`: `Open` for anything, `Object` for a value with members, `Collection` for one with items, and the leaves `Text`, `Number`, `Boolean`, `Date`, `Time`, `DateTime`, `Picture` and `Other`. `Validate` reads it to say what a loop does with a value, which pattern family formats it, whether it sorts and whether a picture frame may bind it; the render prints what it can either way. A leaf of your own metadata is a `ValueShape`: `ValueShape.Text` and the other shared ones, or `new ValueShape(ValueKind.Number, "Amount")` with a name for findings. Give a unit the kind your resolver formats it as, `Picture` to what `Picture` returns bytes for, and `Other` to a unit that prints with the tag's format, such as an identifier.

| Provider | Shape | Resolver | Members |
|---|---|---|---|
| Dictionary, in `Allors.Documents` | `DictionaryShape.Open`, `new DictionaryShape(members)`, `ValueShape` for the leaves | `DictionaryResolver` | The keys of a dictionary, exactly as written, and nothing else. |
| Reflection, in `Allors.Documents.Reflection` | `ReflectionShape.Of<T>()`, `ReflectionShape.Arguments<T>()` | `ReflectionResolver` | Dictionary keys as written; on any other object the name with its first letter upper-cased or as written, as a property, a `Get` method, a `get_` method, a string indexer or a field. The kind follows the type: text and enums, numbers, booleans, dates and times, a byte array as a picture, any other enumerable as a collection, any other class as an object, a `Guid` or a `TimeSpan` as another kind. |

`TryGetMember` returns false for a member the scope does not have, which the renderer reports, and true with a null value for a member that is empty, which prints blank. The renderer never passes a null scope.

To put a resolver of your own in place:

1. Derive from `Resolver`, implement `TryGetMember`, and override `Items`, `IsTrue`, `Format`,
   `Compare` and `Picture` where your value types need it.
2. Implement `IShape` over your metadata, with `Kind` from the unit of a member, `ValueShape` for
   the leaves and `Variants` for an abstract shape, and build the root shape a template renders
   against from the same source.
3. Pass the resolver in `RenderSettings.Resolver` for every render, and the root shape to
   `Validate`.
4. Write a contract test that the shape and the resolver agree: every member the shape declares,
   the resolver reads, a collection in the one is a collection in the other, and a value's kind in
   the shape is how the resolver formats it.
