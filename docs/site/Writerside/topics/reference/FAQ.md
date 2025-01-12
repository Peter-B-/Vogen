# FAQ

## What tests are there
There are unit tests for Vogen itself, and snapshot tests to test that the source code that is generated
matches what is expected.

To run the unit tests for Vogen itself, you can either run them in your IDE, or via `Build.ps1`.
To run the snapshot tests, run `RunSnapshots.ps1`

## I want to add a test, where is the best place for it?

Most tests involve checking two, maybe three, things:
* checking that the source generated is as expected (so the snapshot tests in the main solution)
* checking that the behavior of the change works as expected (so a test in the `consumers` solution (`tests/consumers.sln`))
* (maybe) - if you added/changed the behavior of the code that generates the source code (rather than just changing a template), then a unit test in the main solution

## I've changed the source that is generated, and I now have snapshot failures, what should I do?

There are a **lot** of snapshot tests. A lot of permutations of the following are run:
* framework
* class/struct/record class/record struct
* internal/public/sealed/readonly
* locales
* conversions, such as EF Core, JSON, etc. 

When the tests are run, it uses snapshot tests to compare the current output to the expected output.
If your feature/fix changes the output, the snapshot tests will bring up your configured code diff tool, for instance,
Beyond Compare, and show you the differences. 
If your change modifies what is generated, then it is likely that a lot of `verified` files will need update to match
the new source code that is generated.

To do this, run `RunSnapshots.ps1 -reset`. This will delete all the snapshots and treat what is generated
as the correct version. Needless to say, only do this if you're sure the newly generated code is
correct.

## How do I identify types that are generated by Vogen?
_I'd like to be able to identify types that are generated by Vogen so that I can integrate them in things like EFCore._

This is described in [this how-to page](efcore-tips.md)

## What versions of .NET are supported?

The source generator is .NET Standard 2.0. The code it generates supports all C# language versions from 6.0 and onwards

If you're using the generator in a .NET Framework project and using the old style projects (the one before the 'SDK style' projects), then you'll need to do a few things differently:

* add the reference using `PackageReference` in the `.csproj` file:

```xml
  <ItemGroup>
      <PackageReference Include="Vogen" 
                        Version="[LATEST_VERSION_HERE - E.G. 1.0.18]" 
                        PrivateAssets="all" />
  </ItemGroup>
```

* set the language version to `latest` (or anything `8` or more):

```c#
  <PropertyGroup>
>>  <LangVersion>latest</LangVersion>
    <Configuration Condition=" '$(Configuration)' == '' ">
      Debug
    </Configuration>
```

## Does it support modern features of C#?
This is primarily a source generator. The source it generates is mostly C# 6 for compatibility. But if you use features from a later language version, for instance, `records` from C# 9, then it will also generate records.

Source generation is driven by attributes, and, if you're using .NET 7 or above, the generic version of the `ValueObject` attribute is exposed:

```c#
[ValueObject<int>]
public partial struct Age { }
```

## Why are they called 'Value Objects'?

The term Value Object represents a small object whose equality is based on value and not identity. From [Wikipedia](https://en.wikipedia.org/wiki/Value_object)

> _In computer science, a Value Object is a small object that represents a simple entity whose equality is not based on identity: i.e., two Value Objects are equal when they have the same value, not necessarily being the same object._

In DDD, a Value Object is (again, from [Wikipedia](https://en.wikipedia.org/wiki/Domain-driven_design#Building_blocks))

>  _… a Value Object is an immutable object that contains attributes but has no conceptual identity_

## How can I view the code that is generated?

Add this to your `.csproj` file:

```xml
<PropertyGroup>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>
      Generated
    </CompilerGeneratedFilesOutputPath>
</PropertyGroup>

<ItemGroup>
    <Compile Remove="Generated/*/**/*.cs" />
</ItemGroup>
```

Then, you can view the generated files in the `Generated` folder. In Visual Studio, you need to select 'Show all files' in the Solution Explorer window:

![the solution explorer window shows the 'show all files' option](20220425061514.png)

Here's an example from the included `Samples` project:

![the solution explorer window showing generated files](20220425061733.png)

## Why can't I just use `public record struct CustomerId(int Value);`?

That doesn't give you validation. To validate `Value`, you can't use the shorthand syntax (Primary Constructor). 
So you'd need to do:

```c#
public record struct CustomerId
{
    public CustomerId(int value) {
        if(value <=0) throw new Exception(...)
    }
}
```

You might also provide other constructors which might not validate the data, thereby _allowing invalid data 
into your domain_. Those other constructors might not throw exceptions or might throw different exceptions.  
One of the opinions in Vogen is that any invalid data given to a Value Object throws a `ValueObjectValidationException`.

You could also use `default(CustomerId)` to evade validation.  In Vogen, there are analyzers that catch this and fail the build, e.g.:

```c#
// error VOG009: Type 'CustomerId' cannot be constructed 
// with default as it is prohibited.
CustomerId c = default;
var c2 = default(CustomerId);
```

## Can I serialize and deserialize them?

Yes. By default, each VO is decorated with a `TypeConverter` and `System.Text.Json` (STJ) serializer. There are other converters/serializers for:

* Newtonsoft.Json (NSJ)
* Dapper
* EFCore
* LINQ to DB
* MongoDB/BSON
* Orleans
* ServiceStack.Text
* Protobuf

## Can I use them in EFCore?

Yes, although there are certain considerations. [Please see the EFCore page on the Wiki](https://github.com/SteveDunn/Vogen/wiki/Value-Objects-in-EFCore),
but the TL;DR is:

* If the Value Object on your entity is a struct, then you don't need to do anything special

* But if it is a class, then you need a conversion to be generated, e.g. `[ValueObject<string>(conversions: Conversions.EfCoreValueConverter)]`
  and you need to tell EFCore to use that converter in the `OnModelCreating` method, e.g.:

```c#
        builder.Entity<SomeEntity>(b =>
        {
            b.Property(e => e.Name)
             .HasConversion(new Name.EfCoreValueConverter());
        });
```


### It seems like a lot of overhead; I can validate the value myself when I use it!

You could, but to ensure consistency throughout your domain, you'd have to **validate everywhere**. And Shallow's Law says that that's not possible:

> ⚖️ **Shalloway's Law**
> *"when N things need to change and N > 1, Shalloway will find at most N - 1 of these things."*

Concretely: *"When 5 things need to change, Shalloway will find at most, 4 of these things."*

## If my VO is a `struct`, can I prohibit using `CustomerId customerId = default(CustomerId);`?

**Yes**. The analyzer generates a compilation error.

## If my VO is a `struct`, can I prohibit using `CustomerId customerId = new(CustomerId);`?

**Yes**. The analyzer generates a compilation error.

## If my VO is a struct, can I have my own constructor?

**No**. The parameter-less constructor is generated automatically, and the constructor that takes the underlying value is also generated automatically.

If you add further constructors, then you will get a compilation error from the code generator, e.g.

```c#
[ValueObject(typeof(int))]
public partial struct CustomerId {
    // Vogen already generates this as a private constructor:
    // error CS0111: Type 'CustomerId' already defines a member called
    // 'CustomerId' with the same parameter type
    public CustomerId() { }

    // error VOG008: Cannot have user defined constructors, please use
    // the From method for creation.
    public CustomerId(int value) { }
}
```

## If my VO is a struct, can I have my own fields?

You _could_, but you'd get compiler warning [CS0282-There is no defined ordering between fields in multiple declarations of partial class or struct 'type'](https://docs.microsoft.com/en-us/dotnet/csharp/misc/cs0282)

## Can I explicitly convert to and from the primitive?

Yes, by default Vogen generates explicit operators. See the [casting page](Casting.md) for more information.

## What about implicit casts?

Implicit casts are problematic:

* they bypass validation (since they should never throw an exception)
* they potentially cause a heap allocation
* they bypass normalization
* they weaken type safety

The [casting page](Casting.md) describes these problems in more detail.

## Why is there no interface?

> _"I'd like to tell if a type is a Vogen value object by seeing if it implements an interface, such as `IValidated<T>"`_

**NOTE**
Vogen _can_ now generate an interface, but it's a _[curiously recurring template](https://en.wikipedia.org/wiki/Curiously_recurring_template_pattern)_, and so isn't particularly useful in the same way as a _normal_ interface is and how it would be used in this context

Just like primitives have no interfaces (aside from static abstract interfaces, such as those used in [generic math](https://dunnhq.com/posts/2021/generic-math/)), there's usually no need to have interfaces on value objects. 
The receiver that takes a `CustomerId` knows that it's a value object. 
If it were instead to take (for example), an `IValidated<int>`, then it wouldn't have any more information; 
you'd still have to know to call `Value` to get the value.

It might also relax type-safety. Without the interface, we have signatures such as this:

```c#
public void DoSomething(
  CustomerId customerId, 
  SupplierId supplierId, 
  ProductId productId);
```

… but with the interface, we _could_ have signatures such as this:

```c#
public void DoSomething(
  IValidate<int> customerId, 
  IValidated<int> supplierId, 
  IValidated<int> productId);
```

So, callers could mess things up by calling `DoSomething(productId, supplierId, customerId)`)

There would also be no need to know if it's validated, as, if it's in your domain, **it's valid** (there's no way to manually create invalid instances).  And with that said, there would also be no point in exposing the 'Validate' method via the interface because validation is done at creation.

Having said that, outside the domain, it can be useful to have an interface. The [Guids tutorial](how-to-guides.topic) describes how to generate the interface and when it's useful to do so. 

## Can I represent special values

Yes. You might want to represent special values for things like _invalid_ or _unspecified_ instances, e.g.

```c#
/*
* Instances are the only way to avoid validation, 
* so we can create instances that nobody else can. 
* This is useful for creating special instances 
* that represent concepts such as 'invalid' and 
* 'unspecified'.
*/
[ValueObject]
public readonly partial struct Age
{
    public static readonly Age Unspecified = new(-1);
    public static readonly Age Invalid = new(-2);
    
    private static Validation Validate(int v) => v > 0 
      ? Validation.Ok 
      : Validation.Invalid("Must be zero or more.");
}
```

Using `new` is only permitted in the value object itself and bypassed validation (and normalization).

You can then use default values when using these types, e.g.

```c#
public class Person {
    public Age Age { get; set; } = Age.Unspecified
}
```

… and if you take an Age, you can compare it to an instance that is invalid/unspecified

```c#
public void CanEnter(Age age) {
    if(age == Age.Unspecified || age == Age.Invalid) 
      throw CannotEnterException("Name not specified or is invalid")
    
    return age < 17;
}
```

## Can I normalize the value when a VO is created?
I'd like to normalize/sanitize the values used, for example, trimming the input. Is this possible?

Yes, add NormalizeInput method, e.g.
```c#
    private static string NormalizeInput(string input) => input.Trim();
```
See [the how-to page](NormalizationHowTo.md) for more information.


## Can I create custom Value Object attributes with my own defaults?

No, it used to be possible, but it impacts the performance of Vogen.
A much better way is 
to use [the type alias feature](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-directive#using-alias).

NOTE: *custom attributes must extend a ValueObjectAttribute class; you can’t layer custom attributes on top of each other*

## Why isn't this concept part of the C# language?

It would be great if it was, but it's not there currently. I [wrote an article about it](https://dunnhq.com/posts/2022/non-defaultable-value-types/), but in summary, there is a [long-standing language proposal](https://github.com/dotnet/csharplang/issues/146) focusing on non-defaultable value types.
Having non-defaultable value types is a great first step, but it would also be handy to have something in the language to enforce validation.
So I added a [language proposal for invariant records](https://github.com/dotnet/csharplang/discussions/5574).

One of the responses in the proposal says that the language team decided that validation policies shouldn’t be part of C# but provided by source generators.

## How do I run the benchmarks?

`dotnet run -c Release -- --job short --framework net6.0 --filter *`

## Why do I get a build error when running `.\Build.ps1`?

You might see this:
```
.\Build.ps1 : File C:\Build.ps1 cannot be loaded. The file 
  C:\Build.ps1 is not digitally signed. You cannot run this script 
  on the current system. 
```

To get around this, run `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass`

## What alternatives are there?

[StronglyTypedId](https://github.com/andrewlock/StronglyTypedId)
This is focused more on IDs. Vogen is focused more on 'Domain Concepts' and the constraints associated with those concepts.

[StringlyTyped](https://github.com/stevedunn/stringlytyped)
This is my first attempt and is NON-source-generated. There is memory overhead because the base type is a class. There are also no analyzers. It is now marked as deprecated in favor of Vogen.

[ValueOf](https://github.com/mcintyre321/ValueOf)
Like StringlyTyped - non-source-generated and no analyzers. This is also more relaxed and allows composite 'underlying' types.

[ValueObjectGenerator](https://github.com/RyotaMurohoshi/ValueObjectGenerator)
Like Vogen, but less focused on validation and no code analyzer.

## What primitive types are supported?

Any type (except `ICollection`) can be wrapped. Serialisation and type conversions have implementations for:
* string

* int
* long
* short
* byte

* float (Single)
* decimal
* double

* DateTime
* DateOnly
* TimeOnly
* DateTimeOffset

* Guid

* bool

For other types, generic type conversion and a serializer are applied. If you are supplying your own converters for type
conversion and serialization, then specify `None` for converters and decorate your type with attributes for your own types, e.g.

```c#
[ValueObject<SpecialPrimitive>(conversions: Conversions.None)]
[JsonConverter(typeof(SpecialPrimitiveJsonConverter))]
[TypeConverter(typeof(SpecialPrimitiveTypeConverter))]
public partial struct SpecialMeasurement;
```

Collections aren’t allowed as they don't exhibit value-equality.
It is worth considering if the type you're wrapping in your value object exhibits value-equality.
For types that don't, it completely breaks down the principal of value objects.

## I've made a change that means the 'Snapshot' tests are expectedly failing in the build—what do I do?

Vogen uses a combination of unit tests, in-memory compilation tests, and snapshot tests. The snapshot tests are used
to compare the output of the source generators to the expected output stored on disk.

If your feature/fix changes the output of the source generators, then running the snapshot tests will bring up your
configured code diff tool (for example, Beyond Compare) to show the differences. You can accept the differences in that
tool, or, if there are lots of differences (and they're all expected!), you have various options depending on your
platform and tooling. Those are [described here](https://github.com/VerifyTests/Verify/blob/main/docs/clipboard.md).

**NOTE: If the change to the source generators expectedly changes most of the snapshot output files, then you can tell the
snapshot runner to overwrite the expected files with the actual files that are generated.**

To do this, run `.\RunSnapshots.ps1 -v "minimal" -reset`. 
This deletes all `snaphsot` folders under the `tests` 
folder and treats everything generated as the new baseline for future comparisons.

This will mean there are potentially **thousands** of changed files that will end up in the commit, 
but it's expected and unavoidable.

## How do I debug the source generator?

The easiest way is to debug the SnapshotTests. Put a breakpoint in the code and then debug a test somewhere.

To debug an analyzer, select or write a test in the AnalyzerTests. There are tests that exercise the various analyzers and code-fixers.

## How do I run the tests that actually use the source generator?

It is challenging to run tests that _use_ the source generator in the same project **as** the source generator, so there
is a separate solution for this. It's called `Consumers.sln`. What happens is that `build.ps1` builds the generator, runs
the tests, and creates the NuGet package _in a private local folder_. The package is version `999.9.xxx` and the consumer
references the latest version. The consumer can then _really_ use the source generator, just like anything else.

## Can I get it to throw my own exception?

Yes, by specifying the exception type in either the `ValueObject` attribute or, globally, with `VogenConfiguration`.

## I get an error from Linq2DB when I use a ValueObject that wraps a `TimeOnly` saying that `DateTime` cannot be converted to `TimeOnly`—what should I do?

Linq2DB 4.0 or greater supports `DateOnly` and `TimeOnly`. Vogen generates value converters for Linq2DB; for `DateOnly`, it just works, but for `TimeOnly, you need to add this to your application:

`MappingSchema.Default.SetConverter<DateTime, TimeOnly>(dt => TimeOnly.FromDateTime(dt));`

## Can I use protobuf-net?

Yes. Add a dependency to protobuf-net and set a surrogate attribute:

```c#
[ValueObject(typeof(string))]
[ProtoContract(Surrogate = typeof(string))]
public partial class BoxId;
```

The BoxId type will now be serialized as a `string` in all messages and grpc calls. If one is generating `.proto` files
for other applications from C#, proto files will include the `Surrogate` type as the type.

_thank you to [@DomasM](https://github.com/DomasM) for this information_.

## Can I have a factory method for value objects that wrap GUIDs?

Yes, use the `Customizations.AddFactoryMethodForGuids` in the global config attribute, e.g.

```c#
[assembly: VogenDefaults(
  customizations: Customizations.AddFactoryMethodForGuids)]

[ValueObject<Guid>]
public partial struct CustomerId;

...

var newCustomerId = CustomerId.FromNewGuid();
```

> To customize the generation of Guids, please see [this tutorial](Working-with-IDs.md)
> 

## Can I use value objects instead of primitives as parameters in Blazor pages and components?

You can, but it's not straightforward.
Blazor, unlike ASP.NET Core, doesn't consider type converters.
It passes around and stores parameters as objects,
which means that the casting operators in Blazor are also not considered
(it's not possible to have a cast operator that takes `object` in C#).

There are two solutions:
1. (for components only) - pass the value into the parameter as a value object (parameters can be complex objects)
2. (for pages) override `SetParametersAsync` and convert the primitive to the value object, e.g.

```c#
    public override Task SetParametersAsync(ParameterView parameters)
    {
        if (parameters.TryGetValue<int>("Id", out var x))
        {
            Id = CustomerId.From(x);
        }

        ...
```

## Are value objects any bigger than the primitives that they wrap?
They are by default, but they can be configured so that they're not.

They're bigger because Vogen generates code that stores a field named `_isInitialized`. This
is used to check that the instance is initialized, e.g., after deserializing.
If you don't want that, then you can specify in your project that you don't want validation, e.g.

```xml
<PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    ➕ <DefineConstants>VOGEN_NO_VALIDATION</DefineConstants>
</PropertyGroup>
```

With this set, when we run in debug mode, we can see that there's no size difference:

```c#
[ValueObject]
public readonly partial struct Age;

Console.WriteLine(Marshal.SizeOf<Age>());
Console.WriteLine(Marshal.SizeOf<int>());

// outputs 4, 4
```

In debug builds, Vogen, by default, includes a stack trace field. This is used in the exception when an uninitialized value object is accessed.
If it is important that your debug builds have the same size value objects as your release builds, then add the following in your global config:

```c#
[assembly: VogenDefaults(disableStackTraceRecordingInDebug: true)]
```

## Is it possible to see any diagnostics from the source generator?

Yes, it's possible to see some diagnostic information. Vogen looks for a 'marker type' and if present, generates a file with diagnostic information. Include the following marker type in your project:

```c#
namespace Vogen
{
    public class __ProduceDiagnostics;
}
```

This will produce a `diagnostics.cs` file which you can see in your IDE's solution tree or (in Windows) in a folder something like:
`C:\Users\[user]\AppData\Local\Temp\SourceGeneratedDocuments\[3F61CDF56DC2FDAC8E2336AD]\Vogen\Vogen.ValueObjectGenerator`

The file looks similar to this:

```c#
/*

Generator count: 20
LanguageVersion: CSharp12

User provided global config
===========
UnderlyingType: null
ValidationExceptionType: null
[... etc etc.]

Resolved global config
===========
UnderlyingType: null
ValidationExceptionType: null
[... etc etc.]

Targets
===========
CustomerId
SomethingElseId
SupplierId

*/
```