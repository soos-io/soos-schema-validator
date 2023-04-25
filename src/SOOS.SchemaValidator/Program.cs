using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Schema;
using CommandLine;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace SOOS.SchemaValidator
{
  public static class Program
  {
    private static ProgramOptions _options;

    public static async Task<int> Main(string[] args)
    {
      ParseAndValidateOptionsAsync(args);

      var firstSchemaFile = _options.SchemaFiles.First();

      if (firstSchemaFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
      {
        await ValidateJsonFileAsync();
      }
      else if (firstSchemaFile.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase))
      {
        await ValidateXmlFileAsync();
      }
      else
      {
        throw new ArgumentException("Unknown schema file type.");
      }


      return 0;
    }

    private static async Task ValidateJsonFileAsync()
    {
      if (_options.SchemaFiles.Count() > 1)
      {
        throw new NotImplementedException("TODO multiple local relative schemas instead of just URIs");
      }

      var schemaContent = await File.ReadAllTextAsync(_options.SchemaFiles.First());
      var schema = JSchema.Parse(schemaContent, new JSchemaUrlResolver());

      var jsonContent = await File.ReadAllTextAsync(_options.InputFile);
      var json = JObject.Parse(jsonContent);

      if (json.IsValid(schema, out IList<ValidationError> validationErrors))
      {
        Console.WriteLine("JSON appears to be valid!");
      }
      else
      {
        Console.WriteLine("JSON appears to be INVALID:");
        foreach (var validationError in validationErrors)
        {
          Console.WriteLine($"> {validationError.LineNumber} {validationError.LinePosition} {validationError.Message}");
          if (validationError.ChildErrors != null && validationError.ChildErrors.Count > 0)
          {
            foreach (var childError in validationError.ChildErrors)
            {
              Console.WriteLine($"-->  {childError.LineNumber} {childError.LinePosition} {childError.Message}");
            }
          }
        }
      }
    }

    private static Task ValidateXmlFileAsync()
    {
      var schemaSet = new XmlSchemaSet();
      foreach (var schemaFile in _options.SchemaFiles)
      {
        schemaSet.Add(targetNamespace: null, schemaFile);
      }

      var xDocument = XDocument.Load(_options.InputFile, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
      xDocument.Validate(schemaSet, (o, e) => Console.WriteLine($"> {e.Exception.LineNumber} {e.Exception.LinePosition} {e.Message}"));

      return Task.CompletedTask;
    }

    private static void ParseAndValidateOptionsAsync(string[] args)
    {
      Parser.Default
        .ParseArguments<ProgramOptions>(args)
        .MapResult(parsedOptions => _options = parsedOptions, errors => throw new ArgumentException("Invalid command line option."));

      if (_options.SchemaFiles == null || !_options.SchemaFiles.Any())
      {
        throw new ArgumentException("Empty schema file list.");
      }

      foreach (var schemaFile in _options.SchemaFiles)
      {
        if (!File.Exists(schemaFile))
        {
          throw new FileNotFoundException("The schema file could not be found.", schemaFile);
        }
      }

      if (!File.Exists(_options.InputFile))
      {
        throw new FileNotFoundException("The input file could not be found.", _options.InputFile);
      }
    }

    public class ProgramOptions
    {
      [Option('s', "schema", Required = true, Separator = ';', HelpText = "The path to the schema file(s) used to validate the input. Use semi-colon (;) to add more than one schema.")]
      public IEnumerable<string> SchemaFiles { get; set; }

      [Option('i', "input", Required = true, HelpText = "The path to the file to validate against the schema.")]
      public string InputFile { get; set; }
    }
  }
}