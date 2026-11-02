## Eleven19.IonDotnet

[![Build Status](https://github.com/Eleven19/ion-dotnet/actions/workflows/build.yml/badge.svg)](https://github.com/Eleven19/ion-dotnet/actions/workflows/build.yml)
[![nuget version](https://img.shields.io/nuget/v/Eleven19.IonDotnet)](https://www.nuget.org/packages/Eleven19.IonDotnet)

Eleven19.IonDotnet is the Eleven19-maintained fork of the archived amazon-ion/ion-dotnet
library for reading and writing Amazon Ion in .NET. Amazon deprecated the upstream
library in August 2024; development continues here under the `Eleven19.IonDotnet`
package and namespace.

This package is based on work from Huy Hoang ([dhhoang](https://github.com/dhhoang)) on https://github.com/dhhoang/IonDotnet. The Ion team greatly appreciates Huy's contributions to the Ion community.

### Manual read/write

You can create a `reader` that can read from a (input) stream. You can specify text encoding in the `ReaderOptions`, otherwise Utf8 will be used by default. There are two different `reader`s that you can create, binary and text. Four `reader` objects are created below with different inputs.
```csharp
Stream stream;  //input stream
String text;    //text form
ICatalog catalog;
IIonReader reader;

//create a text reader that automatically detect whether the stream is text/binary
reader = IonReaderBuilder.Build(stream);

//explicitly create a text reader
reader = IonReaderBuilder.Build(stream, new ReaderOptions {Format = ReaderFormat.Text});

//explicitly create a binary reader
reader = IonReaderBuilder.Build(stream, new ReaderOptions {Format = ReaderFormat.Binary});

//create a text reader that reads a string and uses a catalog
reader = IonReaderBuilder.Build(text, new ReaderOptions {Catalog = catalog});
```

Example  of using a  `reader`.
```csharp
/*reader semantics for
{
    number: 1,
    text: "hello world"
}
*/

using (IIonReader reader = IonReaderBuilder.Build(stream))
{
    Console.WriteLine(reader.MoveNext()); // Struct
    reader.StepIn();
    Console.WriteLine(reader.MoveNext()); // Int
    Console.WriteLine(reader.CurrentFieldName); // "number"
    Console.WriteLine(reader.IntValue());   // 1
    Console.WriteLine(reader.MoveNext()); // String
    Console.WriteLine(reader.CurrentFieldName); // "text"
    Console.WriteLine(reader.StringValue());   // "hello world"
    reader.StepOut();
}
```

Similarly you can create a writer that write to a (output) stream. There are two different `writer`s that you can create, binary and text. Three `writer` objects are created below with different inputs.
```csharp
Stream stream; //output stream
var stringWriter = new StringWriter();
IIonWriter writer;
ISymbolTable table1,table2;


//create a text writer that write to the stream.
writer = IonTextWriterBuilder.Build(new StreamWriter(stream));

//create a text writer that write to a stringwriter/builder.
writer = IonTextWriterBuilder.Build(stringWriter);

//create a binary writer using multiple symbol tables
writer = IonBinaryWriterBuilder.Build(stream, new []{table1,table2});

```

Example  of using a  `writer`.
```csharp
/*writer semantics for
{
    number: 1,
    text: "hello world"
}
*/

using (IIonWriter writer = IonTextWriterBuilder.Build(new StreamWriter(stream)))
{
    writer.StepIn(IonType.Struct);
    writer.SetFieldName("number");
    writer.WriteInt(1);
    writer.SetFieldName("text");
    writer.WriteString("hello world");
    writer.StepOut();
    writer.Finish();    //this is important
}
```

### Setup
This repository contains a git submodule called ion-tests, which holds test data used by ion-dotnet's unit tests.
Clone the whole repository and initialize the submodule by:
```
$ git clone --recurse-submodules git@github.com:amzn/ion-dotnet.git
```
Or you can initilize the submodules in the clone
```
$ git submodule init
$ git submodule update
```
The project currently uses default dotnet [CLI](https://docs.microsoft.com/en-us/dotnet/core/tools/?tabs=netcore2x) tools,
you can build the project simply by:
```
$ dotnet build
```

## Publishing

Releases of `Eleven19.IonDotnet` are published to NuGet.org by
`.github/workflows/release.yml`. Update `<Version>` in
`Eleven19.IonDotnet/Eleven19.IonDotnet.csproj`, merge the change into `main`,
then push a matching tag, for example `v0.1.0`.
The workflow checks the version, builds, runs the tests, and publishes the package.
To retry an unpublished release without changing its tag, run the workflow manually
from `main` and supply the existing tag in the `tag` input.

Publishing uses NuGet trusted publishing with the GitHub environment `production`.
Set its `NUGET_USER` variable to the personal NuGet username that created the trust
policy, even when the policy is owned by an organization.
The NuGet policy must allow repository owner `Eleven19`, repository `ion-dotnet`,
workflow file `release.yml`, and environment `production`.
No AWS signing service or long-lived NuGet API key is required.

## License

This project is licensed under the [Apache License, Version 2.0](LICENSE.md).
See [NOTICE](NOTICE) for attribution to Eleven19, Amazon, and Huy Hoang.
The inherited MIT notice for Huy Hoang's original code is preserved in
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
NuGet packages declare `Apache-2.0` and include all three files.

