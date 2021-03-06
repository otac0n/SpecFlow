﻿using System;
using System.Collections.Generic;
using System.Linq;
using TechTalk.SpecFlow.Parser.Gherkin;
using TechTalk.SpecFlow.Parser.SyntaxElements;

namespace TechTalk.SpecFlow.Parser.GherkinBuilder
{
    internal class GherkinParserListener : IGherkinListener
    {
        private readonly string filePath;

        private readonly FeatureBuilder featureBuilder = new FeatureBuilder();
        private StepBuilder stepBuilder;

        private IStepProcessor stepProcessor;
        private ITableProcessor tableProcessor;
        private IExampleProcessor exampleProcessor;

        private IList<Tag> tags = new List<Tag>();

        private readonly List<ErrorDetail> errors = new List<ErrorDetail>();

        private Feature parsedResult;

        public GherkinParserListener(string filePath)
        {
            this.filePath = filePath;
        }

        public List<ErrorDetail> Errors
        {
            get { return errors; }
        }

        public Feature GetResult()
        {
            return parsedResult;
        }

        private Tags FlushTags()
        {
            var retval = tags.Any() ? new Tags(tags.ToArray()) : null;
            tags = new List<Tag>();
            return retval;
        }

        private FilePosition GetFilePosition(GherkinBufferPosition startPosition)
        {
            return new FilePosition(startPosition.Line + 1, startPosition.LinePosition + 1);
        }

        public void Init(GherkinBuffer buffer, bool isPartialScan)
        {
            //nop;
        }

        public void Comment(string commentText, GherkinBufferSpan commentSpan)
        {
            //nop;
        }

        public void Feature(string keyword, string name, string description, GherkinBufferSpan headerSpan, GherkinBufferSpan descriptionSpan)
        {
            featureBuilder.SetHeader(name, description, FlushTags());
            featureBuilder.SourceFilePath = filePath;
        }

        public void Background(string keyword, string name, string description, GherkinBufferSpan headerSpan, GherkinBufferSpan descriptionSpan)
        {
            var background = new BackgroundBuilder(name, description, GetFilePosition(headerSpan.StartPosition));
            stepProcessor = background;
            featureBuilder.AddBackground(background);
        }

        public void Examples(string keyword, string name, string description, GherkinBufferSpan headerSpan, GherkinBufferSpan descriptionSpan)
        {
            var position = GetFilePosition(headerSpan.StartPosition);
            var exampleBuilder = new ExampleBuilder(name, description, position);
            tableProcessor = exampleBuilder;

            if (exampleProcessor == null)
                throw new GherkinSemanticErrorException(
                    "Examples can only be specified for a scenario outline.", position);
            exampleProcessor.ProcessExample(exampleBuilder);
        }

        public void Scenario(string keyword, string name, string description, GherkinBufferSpan headerSpan, GherkinBufferSpan descriptionSpan)
        {
            var currentScenario = new ScenarioBuilder(name, description, FlushTags(), GetFilePosition(headerSpan.StartPosition));
            stepProcessor = currentScenario;
            featureBuilder.AddScenario(currentScenario);
        }

        public void ScenarioOutline(string keyword, string name, string description, GherkinBufferSpan headerSpan, GherkinBufferSpan descriptionSpan)
        {
            var currentScenario = new ScenarioOutlineBuilder(name, description, FlushTags(), GetFilePosition(headerSpan.StartPosition));
            stepProcessor = currentScenario;
            exampleProcessor = currentScenario;
            featureBuilder.AddScenario(currentScenario);
        }

        private void ProcessTag(string name)
        {
            string nameWithoutAt = name.Substring(1);
            tags.Add(new Tag(nameWithoutAt));
        }

        public void FeatureTag(string name, GherkinBufferSpan bufferSpan)
        {
            ProcessTag(name);
        }

        public void ScenarioTag(string name, GherkinBufferSpan bufferSpan)
        {
            ProcessTag(name);
        }

        public void Step(string keyword, StepKeyword stepKeyword, ScenarioBlock scenarioBlock, string text, GherkinBufferSpan stepSpan)
        {
            var position = GetFilePosition(stepSpan.StartPosition);
            stepBuilder = new StepBuilder(stepKeyword, text, position);
            tableProcessor = stepBuilder;

            if (stepProcessor == null)
                throw new GherkinSemanticErrorException(
                    "Steps can only be specified for scenarios or scenario outlines.", position);

            stepProcessor.ProcessStep(stepBuilder);
        }

        private void AssertTableProcessor(FilePosition position)
        {
            if (tableProcessor == null)
                throw new GherkinSemanticErrorException(
                    "Table argument can only be specified for steps and examples.", position);
        }

        public void TableHeader(string[] cells, GherkinBufferSpan rowSpan, GherkinBufferSpan[] cellSpans)
        {
            var position = GetFilePosition(rowSpan.StartPosition);
            AssertTableProcessor(position);

            tableProcessor.ProcessTableRow(cells, position);
        }

        public void TableRow(string[] cells, GherkinBufferSpan rowSpan, GherkinBufferSpan[] cellSpans)
        {
            var position = GetFilePosition(rowSpan.StartPosition);
            AssertTableProcessor(position);

            tableProcessor.ProcessTableRow(cells, position);
        }

        public void MultilineText(string text, GherkinBufferSpan textSpan)
        {
            var position = GetFilePosition(textSpan.StartPosition);
            if (stepBuilder == null)
                throw new GherkinSemanticErrorException(
                    "Multi-line step argument can only be specified for steps.", position);

            stepBuilder.SetMultilineArg(text);
        }

        public void EOF(GherkinBufferPosition eofPosition)
        {
            parsedResult = featureBuilder.GetResult();
        }

        public void Error(string message, GherkinBufferPosition errorPosition, Exception exception)
        {
            var errorDetail = new ErrorDetail
                                  {
                                      Message = message,
                                      Line = errorPosition.Line + 1,
                                      Column = errorPosition.LinePosition + 1
                                  };
            errors.Add(errorDetail);
        }
    }
}