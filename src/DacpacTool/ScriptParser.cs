using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;
using Microsoft.SqlTools.ServiceLayer.BatchParser;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public class ScriptParser : ICommandHandler
    {
        private readonly Parser _parser;
        private bool _parsed;
        private readonly List<string> _includedFileNames = new List<string>();
        private StringBuilder _scriptBuilder = new StringBuilder();
        private string _currentFilePath;

        public ScriptParser(string sourceFile, IVariableResolver variableResolver)
        {
            if (Path.IsPathRooted(sourceFile))
            {
                _currentFilePath = Path.GetDirectoryName(sourceFile);
            }
            else
            {
                sourceFile = Path.Combine(Environment.CurrentDirectory, sourceFile);
                _currentFilePath = Path.GetDirectoryName(sourceFile);
            }

            _parser = new Parser(this, variableResolver, new StreamReader(sourceFile), sourceFile);
        }

        protected void Parse()
        {
            if (! _parsed)
            {
                _parser.Parse();
                _parsed = true;
            }
        }

        public List<string> CollectFileNames()
        {
            Parse();
            return _includedFileNames;
        }

        public string GenerateScript()
        {
            Parse();
            return _scriptBuilder.ToString();
        }

        BatchParserAction ICommandHandler.Go(TextBlock batch, int repeatCount, SqlCmdCommand tokenType)
        {
            batch.GetText(true, out string batchText, out LineInfo lineInfo);
            _scriptBuilder.Append(batchText);
            _scriptBuilder.Append("\nGO\n");
            return BatchParserAction.Continue;
        }

        BatchParserAction ICommandHandler.Include(TextBlock includeBlock, out TextReader stream, out string newFilename)
        {
            includeBlock.GetText(true, out string includedFileName, out LineInfo lineInfo);

            // HACK: Work around access limitations to get access to the underlying tokens
            var tokens = 
                includeBlock.GetType()
                            .GetField("tokens", BindingFlags.NonPublic | BindingFlags.Instance)
                            .GetValue(includeBlock) as IEnumerable<Token>;
            var token = tokens.FirstOrDefault();
            if (token != null)
            {
                _currentFilePath = Path.GetDirectoryName(token.Filename);
            }

            if (!Path.IsPathRooted(includedFileName))
            {
                includedFileName = Path.Combine(_currentFilePath, includedFileName);
            }

            _includedFileNames.Add(includedFileName);

            stream = new StreamReader(includedFileName);
            newFilename = includedFileName;
            return BatchParserAction.Continue;
        }

        BatchParserAction ICommandHandler.OnError(Token token, OnErrorAction action)
        {
            return BatchParserAction.Continue;
        }
    }
}