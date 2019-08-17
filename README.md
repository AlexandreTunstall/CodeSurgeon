# Code Surgeon

A tool for creating & applying patches to assemblies.

## Introduction

When it comes to modifying .NET assemblies, manually doing so is quite easy using tools like [dnSpy](https://github.com/0xd4d/dnSpy).
However, *automatically* modifying .NET assemblies is difficult.
There are two main approaches:

1. Replace the old version of the assembly with the new one.
   * Pros: Easy to do, easy to maintain
   * Cons: Requires a patch for each version of the original, may cause legal issues regarding copyright
2. Write your own patcher to apply the desired changes to the assembly.
   * Pros: Works for similar versions of the original, no copyright issues (disclaimer: not a lawyer)
   * Cons: Hard to do, hard to maintain, requires extra effort for it not to subtly break incompatible versions of the assembly

Code Surgeon aims to be a tool that combines the pros of both approaches without any of the cons.

Example uses:

* Modding games, notably Unity games.
* Exposing internals inside actively updated libraries.

## Usage

First of all, you need to setup your project.

1. [Clone this repository](https://help.github.com/en/articles/cloning-a-repository) (or, if your project uses Git, [add it as a submodule](https://git-scm.com/book/en/v2/Git-Tools-Submodules)).
2. Add the `CodeSurgeon.Attributes` project to your references.

### Code Surgeon Analyzer

This tool comes with a code analyzer to help locate mistakes when writing patch files.
To use it, open the `.csproj` file for your project, and add the following lines.

```XML
<ProjectReference Include="..\CodeSurgeon\CodeSurgeon.Analyzer\CodeSurgeon.Analyzer.csproj">
    <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
    <OutputItemType>Analyzer</OutputItemType>
</ProjectReference>
```

Note that the string for `Include` is the path to the analyzer project's `.csproj` file relative to the directory of the `.csproj` you're editing.
If you're not sure what value to use, add the analyzer as a reference in your IDE, and check what changes are made to the `.csproj` file.

The analyzer assumes that the assembly you're trying to patch and all the dependencies are referenced.
If that's not possible, you can suppress the "unknown dependency" warnings, but you will need to verify the dependencies manually.

### Writing a Patch

All the types used are inside the `CodeSurgeon.Attributes` namespace.

First of all, you need to declare what assembly you're patching.
This is done using the `Patch` attribute.

For example, suppose you wanted to patch `ExtAssembly.dll`.
At the top of one of your source files, you would write the following.

```C#
using CodeSurgeon.Attributes;

[module: Required("ExtAssembly", false)]
```

The `module:` declares that you want to apply this attribute to the entire module (note: modules are usually analogous to assemblies).
The `false` declares that you want to be able to modify the assembly (use `true` if you don't).

Most projects will also depend on other assemblies.
These additional assemblies can be also declared using the `Required` attribute.

For example, if your patch has a read-only dependency `OtherThing.dll`, meaning that it reads types defined in `OtherThing.dll`, you would add the following to your code.

```C#
[module: Required("OtherThing.dll", true)]
```

Patches mainly use three different attributes.

### Mixins

Mixins allow you to modify the value of existing symbols inside the patched assembly.
This can be used to change methods' bodies, or to change fields' values.

For example, the following code instructs the installer to replace the contents of the `Log(string)` method with a `Console.WriteLine` call.

```C#
[Mixin]
internal void Log(string text) => Console.WriteLine(text);
```

The installer will also increase the accessibility to at least match that declared in the mixin.
In the previous example, after installation, the `Log` method will be *at least* `internal`.

### Dependencies

If you need to use a symbol declared inside the patched assembly or one of the required assemblies, you can declare that you need the symbol using the `Dependency` attribute.
This behaves identically to the `Mixin` attribute, except that it will not alter the symbol inside the assembly.
To avoid confusion, it is recommended for all dependencies that must have a method body to throw a `NotImplementedException`.

For example, the following code declares that one of the assemblies must contain a `Person` class containing a property `Name` of type `string` with a getter.

```C#
[Dependency]
internal class Person
{
    [Dependency]
    internal string Name => throw new NotImplementedException();
}
```

### Injections

It is possible to inject symbols without replacing existing symbols using the `Inject` attribute.
The installer will inject the symbol into the patched assembly.

For example, the following code instructs the installer to inject the `Thing` enum into the patched assembly.

```C#
[Inject]
internal enum Thing { }
```

If a symbol with the same name already exists inside the patched assembly, then the installation will fail due to a name collision.

### Base Dependencies

If you wish to modify a method, but want to retain its existing functionality, you can use a base dependency.
Inside a mixin, any call to a method with a `BaseDependency` attribute will be replaced with the method's original body.

For example, the following code instructs the installer to add the `Console.WriteLine` call to the method (instead of rewriting the whole thing as the mixin example does).

```C#
[Mixin]
internal void Log(string text)
{
    Console.WriteLine(text);
    CallBase(text);
}

[BaseDependency]
private void CallBase(string text) => throw new NotImplementedException();
```

Methods with a `BaseDependency` attribute may only be called when inside a mixin method body and when the signature is identical.

### Special Names

Sometimes, the symbol you're trying to a patch has a name that cannot be reproduced in code.
You can use the `Name` attribute to override the name of the associated symbol.

For example, the following code instructs the installer to modify the `World.Person` class instead of the `Person` class.

```C#
[Mixin, Name(typeof(World.Person))]
internal class PersonMixin { }
```

`Name` can also be used without a dependency on the target or with illegal C# names using the `string` or `byte[]` constructors.
If you use these alternate constructors on non-nested types, you must also use the `From` attribute to identify the assembly containing the type you're targeting.
And the following code instructs the installer to modify the `\n` class from the `Obfuscated` assembly, even though `\n` isn't a valid C# class name.

```C#
[Mixin, Name("\n"), From("Obfuscated")]
internal class NewLine { }
```

### Installing a Patch

To actually install your patch into the assembly, you need an installer.

#### With the Provided CLI Installer

Code Surgeon comes with a command line installer in the `CodeSurgeon.CLI` project.
More information on using the installer can be found [here](CodeSurgeon.CLI/README.md).

#### Writing a Custom Installer

Since Code Surgeon provides an API, you may use your own installer instead of the provided one.
