---
title: "Design"
description: "Why the tag language is small: Parr's rules for model-view separation, the model, view, controller and renderer roles, the deliberate steps past StringTemplate, what the library refuses, and why a render never throws."
audience: "Developers"
kind: "Explanation"
status: "Current"
---

> **Status: Current.** Explanation for developers.

Allors Documents keeps its templates deliberately small: a template says where a value goes and
how it prints, and nothing else. This page explains the reasoning, so that a request for a new
tag can be judged against it, and records where the library goes beyond the tradition it comes
from. The rules are Terence Parr's, from
[Enforcing Strict Model-View Separation in Template Engines](https://archives.iw3c2.org/www2004/proceedings/docs/1p224.pdf)
(WWW 2004), and from the twenty years of [StringTemplate](https://www.stringtemplate.org/) that
followed it. The library rendered through StringTemplate until version 3.0 and kept its
discipline when it replaced the engine.

## Strict separation

Parr defines the model and the view as strictly separated when both are encapsulated, the view
cannot modify the model, and the view makes no assumptions about the types of the model's data.
From that he derives five rules for what a template may not do:

1. Change the model. A render has no side effects, so a template may render in any order and any
   number of times.
2. Compute on the model's values. No `price * 0.9` in a template.
3. Compare the model's values. A template may test whether a value is present, absent or empty,
   not whether it is negative; even "print negative amounts in red" belongs in the model.
4. Assume the types of the model's data. No indexing into arrays, no method calls with arguments.
5. Carry layout or display information in the model. The model does not know the template.

He also shows that four constructs are enough to write any template: a reference to an
attribute, conditional inclusion on the presence of an attribute, a reference to another
template, and the application of a template to each item of a multi-valued attribute. The tag
language has the first, the second and the fourth: `<$path>`, `<@if>` with `<@ifdef>`, and
`<@for>`. It has no sub-templates, because a document is its own layout and nothing in it
recurses; a reusable block such as an address is a candidate for a later version.

The reason is who writes templates and where they live. Templates are written in Writer by
people who are not developers, stored in an installation's database, copied between
installations and kept across versions of the application. Logic in them cannot be unit-tested,
reviewed in a pull request or refactored, and the same rule in two templates drifts apart. A
derived value computed in the model is written once, tested once and printed by every template
that names it.

## Model, view, controller and renderer

Parr's answer to where the logic goes is the controller and the renderer, next to the model and
the view: the controller pulls data from the model and pushes it into the view, and formatting
that depends on neither, such as escaping, dates and case, belongs to a renderer the controller
configures. His example: the renderer decides whether a negative number prints as `(23)` or
`-23`; the template decides where on the page the number goes.

| Role | In Allors Documents |
| --- | --- |
| Model | The application's objects as the resolver presents them: their members, derived values such as totals and grouped lines, and the virtual members a resolver adds. Everything that computes, compares or decides lives here. |
| View | The template: layout, static text, styles and pictures, with tags that say where a value goes, what repeats, what is shown or hidden, how a picture takes its frame and which words a map prints. |
| Controller | The code that reads a template and calls `Render` with a model and settings, and that calls `Validate` when a template is saved and in a test over every template. |
| Renderer | The resolver's formatting under the [formatting layers](formatting.md#document-settings): cultures, patterns and the words for true and false, from the library's defaults up to the tag's attributes. Named [renderers](renderers.md) that a tag selects, such as upper-case text or a barcode, and the picture processor that prepares a picture for its frame. |

A tag's `format` attribute is on the view's side of that line: it says how a value prints, not
what it is.

## Steps past StringTemplate

StringTemplate added features for twenty years without moving its line, and refused the ones
that would have: comparisons, arithmetic, variables and method arguments. The tag language takes
its safe additions and goes a few steps further where documents differ from generated code. Each
step is a presentation choice that reads the model and computes nothing on it.

| Addition | Why, and where StringTemplate stands |
| --- | --- |
| `<@else>`, `<@else if>`, `<@else ifdef>` and `not` | StringTemplate 2.0 added else, 2.2 negation and 3.1 else-if. A condition that falls through to a second text is presentation. |
| `sort` on a loop | StringTemplate leaves ordering to the controller. The library's hosts hand it sets, whose order is nobody's, and the order of lines on a page is presentation. The keys are read and compared through the resolver, so the template names them and nothing more. |
| `index` on a loop | StringTemplate's `i`, with a name the author chooses. |
| `separator` and `blank` | StringTemplate's `separator` and `null` options. |
| Zero and white space are false | StringTemplate treats `""` as true. In a document, a zero amount and a blank comment are nothing to show. |
| `<@ifdef>` | A test for the existence of a member, for models whose objects differ in their members. StringTemplate has no such test; it treats a missing property as null. |
| Maps | StringTemplate 2.2's dictionaries: translation tables with literal keys, owned by the view, for titles and labels in the document's language. |
| Pictures by path, with `fit` and `empty` | How a picture takes its frame, and what the frame does without one, are presentation. |

## What the library refuses

Comparisons and equality, arithmetic, variables and assignment, method calls with arguments,
filters on loops, string operations beyond placing two tags next to each other, and template
names or layout taken from the model. Each of these needs is met in the model: a derived value or
a derived class in the application's domain, or a virtual member that a
[resolver](resolve-your-own-model.md) adds without changing the domain. A request for one of them
is a request for a member.

## A render never throws for what a template says

A template mistake prints blank and is reported as a [finding](findings.md); the render
completes. A missing member, a loop that cannot be ordered, a value that is no picture, a tag
that cannot be read: each has a defined result and a finding that names the tag, the part and the
paragraph. Only a template whose blocks cannot be rendered, because a block never ends or would
cut a table, is refused at `Render`, with the same findings, which `Validate` reports before.

The reason is where mistakes should surface. A document run that fails on one typo is worse than
a document with one blank and a report that says where, and the typo should not reach a run at
all. `Validate` reports everything the template and the shape of its model can tell, before any
data exists, so an application runs it when a template is saved and in a test over every template
it ships. StringTemplate 4 made the same choice with its error listener; template engines that
offer a strict mode leave the choice to the caller.

## Settings in layers

The library's defaults, the caller's settings, the template's document settings and the tag's
attributes apply in that order, and the last layer wins. The template beats the caller because
the template belongs to the installation and travels with it: a Belgian template prints Belgian
numbers whichever server renders it. An application that must force how a value prints gives the
value formatted, in the model. The [Formatting](formatting.md) page has the rules, including how
the cultures per kind combine.

## A core without dependencies

The core package has no third-party dependencies and uses no reflection, targets
`netstandard2.0` and `net10.0`, and is trimmable and AOT-compatible, so that it can sit in any
host. Reflection over plain classes lives in `Allors.Documents.Reflection`; a host with an object
model of its own implements the resolver and the shape instead. Capabilities that need a library
of their own, such as barcodes or image scaling, come as optional [packages](renderers.md)
on the renderer and picture processing extension points, so that the core stays as it is. An
application that has such a capability wraps its own code on the same extension points and
needs no package.
