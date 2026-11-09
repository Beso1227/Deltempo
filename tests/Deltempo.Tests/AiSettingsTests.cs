using System;
using System.Collections.Generic;
using System.Linq;
using WinTempCleaner.Services;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// The AI settings rules both settings surfaces share, and the per-provider key store
/// they write to. These are pure: no network, no settings file, no DPAPI.
/// </summary>
public class AiSettingsTests
{
    // -----------------------------------------------------------------
    // AiSettingsValidator
    // -----------------------------------------------------------------

    [Fact]
    public void Validator_BuiltInNeedsNoKeyAndNoEndpoint()
    {
        IReadOnlyList<AiSettingsIssue> issues = AiSettingsValidator.Validate(
            "BuiltIn", apiKey: null, ollamaEndpoint: null);

        Assert.Empty(issues);
        Assert.True(AiSettingsValidator.IsValid(issues));
        Assert.Equal(string.Empty, AiSettingsValidator.Describe(issues));
    }

    [Fact]
    public void Validator_CloudProviderWithoutAKeyIsABlockingError()
    {
        IReadOnlyList<AiSettingsIssue> issues = AiSettingsValidator.Validate(
            "Gemini", apiKey: "  ", ollamaEndpoint: null);

        AiSettingsIssue issue = Assert.Single(issues);
        Assert.Equal(AiSettingsValidator.FieldApiKey, issue.Field);
        Assert.True(issue.IsError);
        Assert.False(AiSettingsValidator.IsValid(issues));
        Assert.Contains("Gemini", issue.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Gemini")]
    [InlineData("Groq")]
    [InlineData("OpenAI")]
    [InlineData("OpenRouter")]
    public void Validator_EveryHostedProviderAcceptsAKey(string provider)
    {
        IReadOnlyList<AiSettingsIssue> issues = AiSettingsValidator.Validate(
            provider, apiKey: "test-key", ollamaEndpoint: "not a url");

        Assert.True(AiSettingsValidator.IsValid(issues));
        Assert.DoesNotContain(issues, i => i.Field == AiSettingsValidator.FieldEndpoint);
    }

    [Fact]
    public void Validator_OllamaNeedsNoKeyAndAcceptsTheDefaultEndpoint()
    {
        IReadOnlyList<AiSettingsIssue> issues = AiSettingsValidator.Validate(
            "Ollama", apiKey: null, ollamaEndpoint: "http://localhost:11434");

        Assert.Empty(issues);
    }

    [Fact]
    public void Validator_OllamaRejectsANonHttpEndpoint()
    {
        IReadOnlyList<AiSettingsIssue> issues = AiSettingsValidator.Validate(
            "Ollama", apiKey: null, ollamaEndpoint: "localhost:11434");

        AiSettingsIssue issue = Assert.Single(issues);
        Assert.Equal(AiSettingsValidator.FieldEndpoint, issue.Field);
        Assert.True(issue.IsError);
    }

    [Fact]
    public void Validator_ModelNameWithASpaceIsAWarningNotAnError()
    {
        IReadOnlyList<AiSettingsIssue> issues = AiSettingsValidator.Validate(
            "OpenAI", apiKey: "test-key", ollamaEndpoint: null, modelName: "gpt 5.4");

        AiSettingsIssue issue = Assert.Single(issues);
        Assert.Equal(AiSettingsValidator.FieldModel, issue.Field);
        Assert.False(issue.IsError);
        Assert.True(AiSettingsValidator.IsValid(issues));
    }

    [Fact]
    public void Validator_AutoQueryWhileAiIsOffIsAWarningNotAnError()
    {
        IReadOnlyList<AiSettingsIssue> issues = AiSettingsValidator.Validate(
            "BuiltIn",
            apiKey: null,
            ollamaEndpoint: null,
            onlineAiEnabled: false,
            autoQueryLargeFiles: true);

        AiSettingsIssue issue = Assert.Single(issues);
        Assert.Equal(AiSettingsValidator.FieldAutoQuery, issue.Field);
        Assert.False(issue.IsError);
        Assert.True(AiSettingsValidator.IsValid(issues));
    }

    [Fact]
    public void Validator_DisabledAiDoesNotBlockSavingADormantProvider()
    {
        // "Turn AI off, then press Save" must not be refused because the provider that
        // is no longer called still has no key. Re-enabling AI validates it again.
        IReadOnlyList<AiSettingsIssue> issues = AiSettingsValidator.Validate(
            "Gemini",
            apiKey: string.Empty,
            ollamaEndpoint: null,
            onlineAiEnabled: false);

        Assert.Empty(issues);
        Assert.True(AiSettingsValidator.IsValid(issues));
    }

    [Fact]
    public void Validator_UnknownProviderIsAnErrorAndStopsThere()
    {
        IReadOnlyList<AiSettingsIssue> issues = AiSettingsValidator.Validate(
            "NotAProvider", apiKey: null, ollamaEndpoint: null);

        AiSettingsIssue issue = Assert.Single(issues);
        Assert.Equal(AiSettingsValidator.FieldProvider, issue.Field);
        Assert.True(issue.IsError);
    }

    [Fact]
    public void Validator_DescribeReportsTheBlockingErrorBeforeAWarning()
    {
        IReadOnlyList<AiSettingsIssue> issues = AiSettingsValidator.Validate(
            "Gemini",
            apiKey: string.Empty,
            ollamaEndpoint: null,
            modelName: "bad model",
            onlineAiEnabled: true,
            autoQueryLargeFiles: true);

        Assert.Equal(2, issues.Count);
        Assert.True(issues[0].IsError);
        Assert.Equal(issues[0].Message, AiSettingsValidator.Describe(issues));
    }

    [Fact]
    public void Validator_IssuesAreOrderedErrorsFirst()
    {
        IReadOnlyList<AiSettingsIssue> issues = AiSettingsValidator.Validate(
            "Ollama",
            apiKey: null,
            ollamaEndpoint: "ftp://localhost:11434",
            modelName: "bad model");

        Assert.Equal(2, issues.Count);
        Assert.True(issues[0].IsError);
        Assert.False(issues[1].IsError);
    }

    // -----------------------------------------------------------------
    // Per-provider key store
    // -----------------------------------------------------------------

    [Fact]
    public void ApiKeys_EachProviderKeepsItsOwnCredential()
    {
        var settings = new AppSettings { AiProvider = "Gemini" };
        settings.SetAiApiKey("Gemini", "gemini-key");
        settings.SetAiApiKey("Groq", "groq-key");

        settings.AiProvider = "Groq";

        Assert.Equal("gemini-key", settings.GetAiApiKey("Gemini"));
        Assert.Equal("groq-key", settings.GetAiApiKey("Groq"));
        Assert.Equal("groq-key", settings.GetAiApiKey());
        Assert.Equal(string.Empty, settings.GetAiApiKey("OpenAI"));
    }

    [Fact]
    public void ApiKeys_LookupIsCaseInsensitive()
    {
        var settings = new AppSettings { AiProvider = "Gemini" };
        settings.SetAiApiKey("gemini", "gemini-key");

        Assert.Equal("gemini-key", settings.GetAiApiKey("Gemini"));
        Assert.Equal("gemini-key", settings.GetAiApiKey("GEMINI"));
    }

    [Fact]
    public void ApiKeys_EmptyValueRemovesTheEntry()
    {
        var settings = new AppSettings { AiProvider = "OpenAI" };
        settings.SetAiApiKey("OpenAI", "openai-key");
        settings.SetAiApiKey("OpenAI", "   ");

        Assert.Equal(string.Empty, settings.GetAiApiKey("OpenAI"));
        Assert.False(settings.AiApiKeys.ContainsKey("OpenAI"));
    }

    [Fact]
    public void ApiKeys_LegacySingleKeyOnlyServesTheProviderItWasEnteredFor()
    {
        // A settings file written before the per-provider map existed: one key, one
        // provider. It must keep working for that provider and must never leak to
        // another one, which is the bug that produced a 401 after a provider switch.
        var settings = new AppSettings
        {
            AiProvider = "Gemini",
            AiApiKey = "legacy-gemini-key",
        };

        Assert.Equal("legacy-gemini-key", settings.GetAiApiKey("Gemini"));
        Assert.Equal("legacy-gemini-key", settings.GetAiApiKey());
        Assert.Equal(string.Empty, settings.GetAiApiKey("Groq"));
    }

    [Fact]
    public void ApiKeys_MapWinsOverTheLegacyMirror()
    {
        var settings = new AppSettings
        {
            AiProvider = "Gemini",
            AiApiKey = "legacy-gemini-key",
        };
        settings.SetAiApiKey("Gemini", "current-gemini-key");

        Assert.Equal("current-gemini-key", settings.GetAiApiKey("Gemini"));
        Assert.Equal("current-gemini-key", settings.AiApiKey);
    }

    [Fact]
    public void ApiKeys_RemovingTheSelectedProviderClearsTheLegacyMirror()
    {
        var settings = new AppSettings { AiProvider = "Groq" };
        settings.SetAiApiKey("Groq", "groq-key");
        Assert.Equal("groq-key", settings.AiApiKey);

        settings.SetAiApiKey("Groq", string.Empty);

        Assert.Equal(string.Empty, settings.AiApiKey);
        Assert.Equal(string.Empty, settings.GetAiApiKey("Groq"));
    }

    [Fact]
    public void ApiKeys_RemovingAnotherProviderLeavesTheMirrorAlone()
    {
        var settings = new AppSettings { AiProvider = "Groq" };
        settings.SetAiApiKey("Groq", "groq-key");
        settings.SetAiApiKey("Gemini", "gemini-key");
        settings.SetAiApiKey("Gemini", string.Empty);

        Assert.Equal("groq-key", settings.AiApiKey);
        Assert.Equal("groq-key", settings.GetAiApiKey("Groq"));
    }

    // -----------------------------------------------------------------
    // Catalog: one list for both settings surfaces
    // -----------------------------------------------------------------

    [Fact]
    public void Catalog_ProviderOptionsCoverEveryKnownProvider()
    {
        string[] optionIds = AiModelCatalog.GetProviderOptions().Select(o => o.Id).ToArray();

        Assert.Equal(AiModelCatalog.ProviderNames.Count, optionIds.Length);
        foreach (string provider in AiModelCatalog.ProviderNames)
        {
            Assert.Contains(provider, optionIds, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Catalog_ProviderOptionsHaveUniqueLabelsAndADescription()
    {
        IReadOnlyList<AiProviderOption> options = AiModelCatalog.GetProviderOptions();

        Assert.Equal(options.Count, options.Select(o => o.Label).Distinct(StringComparer.Ordinal).Count());
        Assert.All(options, option =>
        {
            Assert.False(string.IsNullOrWhiteSpace(option.Label));
            Assert.False(string.IsNullOrWhiteSpace(option.Description));
        });
    }

    [Fact]
    public void Catalog_ProviderLabelsDoNotNameARetiredModel()
    {
        foreach (AiProviderOption option in AiModelCatalog.GetProviderOptions())
        {
            Assert.DoesNotContain("GPT-4o", option.Label, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("gemini-2.0", option.Label, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Catalog_ProviderLabelsNeverNameAModel()
    {
        // A model in a provider label is stale the day it retires; the picker beside it
        // already lists the provider's live models.
        foreach (AiProviderOption option in AiModelCatalog.GetProviderOptions())
        {
            Assert.True(
                AiModelCatalog.TryGetProvider(option.Id, out AiProviderDescriptor descriptor),
                $"Provider option '{option.Id}' has no catalog descriptor.");

            if (!string.IsNullOrEmpty(descriptor.DefaultModelId))
            {
                Assert.DoesNotContain(descriptor.DefaultModelId, option.Label, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void Catalog_KeyRequirementMatchesTheProvider()
    {
        foreach (string provider in new[] { "BuiltIn", "Ollama" })
        {
            Assert.True(AiModelCatalog.TryGetProvider(provider, out AiProviderDescriptor descriptor));
            Assert.False(descriptor.RequiresApiKey, $"{provider} is local and must not ask for a key.");
        }

        foreach (string provider in new[]
        {
            "OpenAI", "Anthropic", "Gemini", "xAI", "OpenRouter", "Groq",
            "Mistral", "Cerebras", "DeepSeek", "GitHubModels", "HuggingFace", "Together",
        })
        {
            Assert.True(AiModelCatalog.TryGetProvider(provider, out AiProviderDescriptor descriptor));
            Assert.True(descriptor.RequiresApiKey, $"{provider} is hosted and must ask for a key.");
        }
    }

    [Fact]
    public void Catalog_DescribeProviderAndModelAgreeWithTheCatalog()
    {
        Assert.Equal(
            "GPT models. Paid per use. The file summary is sent to OpenAI.",
            AiModelCatalog.DescribeProvider("OpenAI"));
        Assert.Equal(string.Empty, AiModelCatalog.DescribeProvider("NotAProvider"));

        Assert.Equal("Built-in engine - no model call.", AiModelCatalog.DescribeModel("BuiltIn", null));
        Assert.Equal("gpt-5.4-mini (provider default)", AiModelCatalog.DescribeModel("OpenAI", null));
        Assert.Equal("my-model (custom)", AiModelCatalog.DescribeModel("OpenAI", "my-model"));
    }

    // -----------------------------------------------------------------
    // Transport routing
    // -----------------------------------------------------------------

    [Theory]
    [InlineData("OpenAI", "api.openai.com")]
    [InlineData("openai", "api.openai.com")]
    [InlineData("Anthropic", "api.anthropic.com")]
    [InlineData("xAI", "api.x.ai")]
    [InlineData("Groq", "api.groq.com")]
    [InlineData("OpenRouter", "openrouter.ai")]
    [InlineData("Mistral", "api.mistral.ai")]
    [InlineData("Cerebras", "api.cerebras.ai")]
    [InlineData("DeepSeek", "api.deepseek.com")]
    [InlineData("GitHubModels", "models.github.ai")]
    [InlineData("HuggingFace", "router.huggingface.co")]
    [InlineData("Together", "api.together.xyz")]
    public void Transport_HostedProvidersResolveToTheirOwnHost(string provider, string host)
    {
        Assert.Contains(
            host,
            AiProviderClient.ResolveOpenAiEndpoint(provider),
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("OpenAI", "api.openai.com")]
    [InlineData("Anthropic", "api.anthropic.com")]
    [InlineData("xAI", "api.x.ai")]
    [InlineData("Groq", "api.groq.com")]
    [InlineData("OpenRouter", "openrouter.ai")]
    [InlineData("Mistral", "api.mistral.ai")]
    [InlineData("Cerebras", "api.cerebras.ai")]
    [InlineData("DeepSeek", "api.deepseek.com")]
    [InlineData("GitHubModels", "models.github.ai")]
    [InlineData("HuggingFace", "router.huggingface.co")]
    [InlineData("Together", "api.together.xyz")]
    public void Transport_ModelListsComeFromTheSameProviderHost(string provider, string host)
    {
        // A chat endpoint on the right host with a model list on another host is how a
        // picker offers models the account cannot actually call.
        Assert.Contains(
            host,
            AiProviderClient.ResolveModelsEndpoint(provider),
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("OpenAI")]
    [InlineData("Groq")]
    [InlineData("OpenRouter")]
    public void Transport_CallerSuppliedOllamaHostCannotRedirectAHostedProvider(string provider)
    {
        // Every intelligence service passes settings.AiOllamaEndpoint on every request.
        // Honouring it in the OpenAI-compatible transport sent these providers to
        // http://localhost:11434, which surfaced as "provider unreachable".
        var request = new AiChatRequest(
            Provider: provider,
            ApiKey: "test-key",
            ModelId: "test-model",
            SystemPrompt: "sys",
            UserPrompt: "user",
            OllamaEndpoint: "http://localhost:11434");

        string endpoint = AiProviderClient.ResolveOpenAiEndpoint(request);

        Assert.DoesNotContain("localhost", endpoint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("11434", endpoint, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------
    // Model discovery
    // -----------------------------------------------------------------

    [Fact]
    public void ModelList_OpenAiShape_ReadsSortedDataIds()
    {
        const string body = """
            {"object":"list","data":[{"id":"gpt-5.4-mini"},{"id":"gpt-4o-mini"}]}
            """;

        IReadOnlyList<AiModelOption> models = AiProviderClient.ParseModelList("OpenAI", body);

        Assert.Equal(["gpt-4o-mini", "gpt-5.4-mini"], models.Select(m => m.Id).ToArray());

        // OpenAI's /models returns ids only. The picker must read like a name list, not wire data.
        Assert.Equal(["GPT-4o mini", "GPT-5.4 mini"], models.Select(m => m.DisplayName).ToArray());
    }

    [Fact]
    public void ModelList_OpenRouterShape_PrefersTheHumanName()
    {
        const string body = """
            {"data":[
              {"id":"openai/gpt-4o","name":"OpenAI: GPT-4o"},
              {"id":"meta-llama/llama-3.3-70b-instruct","name":"Meta: Llama 3.3 70B Instruct"}
            ]}
            """;

        IReadOnlyList<AiModelOption> models = AiProviderClient.ParseModelList("OpenRouter", body);

        Assert.Equal(2, models.Count);
        Assert.Equal("meta-llama/llama-3.3-70b-instruct", models[0].Id);
        Assert.Equal("Meta: Llama 3.3 70B Instruct", models[0].DisplayName);
        Assert.Equal("openai/gpt-4o", models[1].Id);
    }

    [Fact]
    public void ModelList_GeminiShape_KeepsOnlyChatCapableModels()
    {
        const string body = """
            {"models":[
              {"name":"models/gemini-2.5-flash","displayName":"Gemini 2.5 Flash",
               "supportedGenerationMethods":["generateContent","countTokens"]},
              {"name":"models/text-embedding-004","displayName":"Text Embedding 004",
               "supportedGenerationMethods":["embedContent"]},
              {"name":"models/legacy-no-methods","displayName":"Legacy"}
            ]}
            """;

        IReadOnlyList<AiModelOption> models = AiProviderClient.ParseModelList("Gemini", body);

        AiModelOption only = Assert.Single(models);
        // The wire id drops the "models/" prefix: that is what a chat request expects.
        Assert.Equal("gemini-2.5-flash", only.Id);
        Assert.Equal("Gemini 2.5 Flash", only.DisplayName);
    }

    [Fact]
    public void ModelList_OllamaShape_ReadsLocalTags()
    {
        const string body = """
            {"models":[{"name":"qwen2.5:7b"},{"name":"llama3.2:latest"}]}
            """;

        IReadOnlyList<AiModelOption> models = AiProviderClient.ParseModelList("Ollama", body);

        Assert.Equal(["llama3.2:latest", "qwen2.5:7b"], models.Select(m => m.Id).ToArray());

        // The ":latest" tag is noise in a label; the catalog name wins where it knows the id.
        Assert.Equal(["Llama 3.2", "Qwen2.5 7B"], models.Select(m => m.DisplayName).ToArray());
    }

    [Fact]
    public void ModelList_AnthropicShape_ReadsDisplayName()
    {
        // Anthropic's /v1/models wraps rows in "data" and spells the label display_name.
        const string body = """
            {"data":[
              {"type":"model","id":"claude-sonnet-4-5","display_name":"Claude Sonnet 4.5"},
              {"type":"model","id":"claude-haiku-4-5","display_name":"Claude Haiku 4.5"}
            ],"has_more":false}
            """;

        IReadOnlyList<AiModelOption> models = AiProviderClient.ParseModelList("Anthropic", body);

        Assert.Equal(
            ["claude-haiku-4-5", "claude-sonnet-4-5"],
            models.Select(m => m.Id).ToArray());
        Assert.Equal(
            ["Claude Haiku 4.5", "Claude Sonnet 4.5"],
            models.Select(m => m.DisplayName).ToArray());
    }

    [Fact]
    public void ModelList_GitHubModelsShape_ReadsABareArray()
    {
        // GitHub Models answers with a top-level array, not {"data":[...]}.
        const string body = """
            [
              {"id":"openai/gpt-4o-mini","name":"GPT-4o mini","publisher":"OpenAI",
               "supported_output_modalities":["text"]},
              {"id":"openai/text-embedding-3-small","name":"Text Embedding 3 Small","publisher":"OpenAI"}
            ]
            """;

        IReadOnlyList<AiModelOption> models = AiProviderClient.ParseModelList("GitHubModels", body);

        AiModelOption only = Assert.Single(models);
        Assert.Equal("openai/gpt-4o-mini", only.Id);
        Assert.Equal("GPT-4o mini", only.DisplayName);
    }

    [Fact]
    public void ModelList_TogetherShape_ReadsABareArrayAndDropsNonChatTypes()
    {
        const string body = """
            [
              {"id":"meta-llama/Llama-3.3-70B-Instruct-Turbo","type":"chat",
               "display_name":"Llama 3.3 70B Instruct Turbo"},
              {"id":"BAAI/bge-large-en-v1.5","type":"embedding","display_name":"BGE Large EN"}
            ]
            """;

        IReadOnlyList<AiModelOption> models = AiProviderClient.ParseModelList("Together", body);

        AiModelOption only = Assert.Single(models);
        Assert.Equal("meta-llama/Llama-3.3-70B-Instruct-Turbo", only.Id);
        Assert.Equal("Llama 3.3 70B Instruct Turbo", only.DisplayName);
    }

    [Fact]
    public void ModelList_NonOpenRouterPricingIsNotGuessed()
    {
        // Together quotes per million tokens, OpenRouter per token. Reading both as
        // per-token produced "$880000/M" labels, so only OpenRouter's pricing is read.
        const string body = """
            [{"id":"some-chat-model","type":"chat","pricing":{"input":0.88,"output":0.88}}]
            """;

        IReadOnlyList<AiModelOption> models = AiProviderClient.ParseModelList("Together", body);

        AiModelOption only = Assert.Single(models);
        Assert.Null(only.Badge);
        Assert.Null(only.Note);
    }

    [Fact]
    public void ModelList_DuplicateIdsCollapseIgnoringCase()
    {
        const string body = """
            {"data":[{"id":"gpt-4o-mini"},{"id":"GPT-4o-mini"},{"id":"gpt-4o-mini"}]}
            """;

        IReadOnlyList<AiModelOption> models = AiProviderClient.ParseModelList("OpenAI", body);

        Assert.Single(models);
    }

    [Fact]
    public void ModelList_NotJson_IsReportedAsMalformedResponse()
    {
        AiProviderException ex = Assert.Throws<AiProviderException>(
            () => AiProviderClient.ParseModelList("OpenRouter", "<html>gateway error</html>"));

        Assert.Equal(AiFailureKind.MalformedResponse, ex.Kind);
        Assert.Contains("OpenRouter", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ModelList_EmptyPayload_ReturnsEmptyListNotAnError()
    {
        IReadOnlyList<AiModelOption> models = AiProviderClient.ParseModelList("OpenAI", "{}");

        Assert.Empty(models);
    }

    [Fact]
    public void ModelList_OpenAiShape_DropsModelsThatCannotAnswerChat()
    {
        // /models publishes the whole catalogue. Offering an embedding, speech or image
        // model in a chat picker guaranteed a 400 the moment it was selected.
        const string body = """
            {"data":[
              {"id":"gpt-5.4-mini"},
              {"id":"text-embedding-3-small"},
              {"id":"whisper-1"},
              {"id":"tts-1"},
              {"id":"dall-e-3"},
              {"id":"omni-moderation-latest"},
              {"id":"gpt-4o-realtime-preview"},
              {"id":"gpt-4o-audio-preview"},
              {"id":"gpt-image-1"}
            ]}
            """;

        IReadOnlyList<AiModelOption> models = AiProviderClient.ParseModelList("OpenAI", body);

        Assert.Equal(["gpt-5.4-mini"], models.Select(m => m.Id).ToArray());
    }

    [Fact]
    public void ModelList_OpenRouterShape_DropsBatchVariants()
    {
        // :batch ids are asynchronous jobs, not chat completions.
        const string body = """
            {"data":[
              {"id":"google/gemma-4-31b-it:free"},
              {"id":"meta-llama/llama-3.3-70b-instruct:batch"}
            ]}
            """;

        IReadOnlyList<AiModelOption> models = AiProviderClient.ParseModelList("OpenRouter", body);

        Assert.Equal(["google/gemma-4-31b-it:free"], models.Select(m => m.Id).ToArray());
    }

    [Fact]
    public void ModelList_OpenRouterShape_DropsModelsThatCannotEmitText()
    {
        // A model whose output modality is an image cannot answer this app's JSON request.
        const string body = """
            {"data":[
              {"id":"text-model","name":"Text Model","architecture":{"output_modalities":["text"]}},
              {"id":"image-model","name":"Image Model","architecture":{"output_modalities":["image"]}}
            ]}
            """;

        IReadOnlyList<AiModelOption> models = AiProviderClient.ParseModelList("OpenRouter", body);

        Assert.Equal(["text-model"], models.Select(m => m.Id).ToArray());
    }

    [Fact]
    public void ModelList_OpenRouterShape_BadgesFreeModelsAndSortsThemFirst()
    {
        // A zero-credit OpenRouter account can only call the free models; the picker is
        // where that distinction has to be visible, because a paid model answers 402.
        const string body = """
            {"data":[
              {"id":"meta-llama/llama-3.3-70b-instruct","name":"Meta: Llama 3.3 70B",
               "pricing":{"prompt":"0.00000059","completion":"0.00000079"}},
              {"id":"google/gemma-4-31b-it:free","name":"Google: Gemma 4 31B (free)",
               "pricing":{"prompt":"0","completion":"0"}}
            ]}
            """;

        IReadOnlyList<AiModelOption> models = AiProviderClient.ParseModelList("OpenRouter", body);

        Assert.Equal(2, models.Count);
        Assert.Equal("google/gemma-4-31b-it:free", models[0].Id);
        Assert.Equal("FREE", models[0].Badge);
        Assert.Equal("Google: Gemma 4 31B (free) - FREE", models[0].PickerLabel);

        AiModelOption paid = models[1];
        Assert.Null(paid.Badge);
        Assert.Equal("Meta: Llama 3.3 70B", paid.PickerLabel);
        Assert.Contains("$0.59/M in", paid.Note!, StringComparison.Ordinal);
        Assert.Contains("$0.79/M out", paid.Note!, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeFetchedModels_ReportsTheCountWhenNothingIsWrong()
    {
        AiModelOption[] models =
        [
            new("model-a", "Model A"),
            new("model-b", "Model B"),
        ];

        string status = AiModelCatalog.DescribeFetchedModels("OpenAI", models, "model-a");

        Assert.Equal("2 models available to this key.", status);
    }

    [Fact]
    public void DescribeFetchedModels_ExplainsAnEmptyList()
    {
        string status = AiModelCatalog.DescribeFetchedModels("OpenRouter", [], null);

        Assert.Contains("returned no models", status, StringComparison.Ordinal);
        Assert.Contains("OpenRouter", status, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeFetchedModels_WarnsWhenTheSelectedModelIsPaid()
    {
        AiModelOption[] models =
        [
            new("free-model", "Free Model", "No credit needed", "FREE"),
            new("paid-model", "Paid Model", "$1/M in"),
        ];

        string status = AiModelCatalog.DescribeFetchedModels("OpenRouter", models, "paid-model");

        // The picker hides the paid row, so the line counts what is actually offered and
        // explains why the saved choice is no longer in the list.
        Assert.Contains("1 free model available", status, StringComparison.Ordinal);
        Assert.Contains("paid", status, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FREE", status, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeFetchedModels_IsQuietWhenTheSelectedModelIsFreeOrUnknown()
    {
        AiModelOption[] models =
        [
            new("free-model", "Free Model", "No credit needed", "FREE"),
            new("paid-model", "Paid Model", "$1/M in"),
        ];

        string freeStatus = AiModelCatalog.DescribeFetchedModels("OpenRouter", models, "free-model");
        Assert.Contains("1 free model available", freeStatus, StringComparison.Ordinal);
        Assert.DoesNotContain("saved model is paid", freeStatus, StringComparison.OrdinalIgnoreCase);

        // An id the provider did not return is not called paid; the line says it is not in
        // the free list instead, which is the only thing the provider actually proved.
        string unknownStatus =
            AiModelCatalog.DescribeFetchedModels("OpenRouter", models, "some-other-model");
        Assert.DoesNotContain("saved model is paid", unknownStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not in this list", unknownStatus, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------
    // Why an Ask-AI request cannot run
    // -----------------------------------------------------------------

    [Fact]
    public void AiUnavailableReason_NamesTheMasterSwitchWhenItIsOff()
    {
        AppSettings saved = SettingsService.Current;
        bool originalEnabled = saved.EnableOnlineAiSafety;
        string originalProvider = saved.AiProvider;
        string originalKey = saved.GetAiApiKey("OpenRouter");

        try
        {
            SettingsService.Update(s =>
            {
                s.EnableOnlineAiSafety = false;
                s.AiProvider = "OpenRouter";
                s.SetAiApiKey("OpenRouter", "test-key");
            });

            string? reason = LargeFileHunterService.GetAiUnavailableReason();

            Assert.NotNull(reason);
            Assert.Contains("off", reason!, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("OpenRouter", reason!, StringComparison.Ordinal);
        }
        finally
        {
            SettingsService.Update(s =>
            {
                s.EnableOnlineAiSafety = originalEnabled;
                s.AiProvider = originalProvider;
                s.SetAiApiKey("OpenRouter", originalKey);
            });
        }
    }

    [Fact]
    public void AiUnavailableReason_NamesTheMissingKey()
    {
        AppSettings saved = SettingsService.Current;
        bool originalEnabled = saved.EnableOnlineAiSafety;
        string originalProvider = saved.AiProvider;
        string originalKey = saved.GetAiApiKey("OpenRouter");

        try
        {
            SettingsService.Update(s =>
            {
                s.EnableOnlineAiSafety = true;
                s.AiProvider = "OpenRouter";
                s.SetAiApiKey("OpenRouter", string.Empty);
            });

            string? reason = LargeFileHunterService.GetAiUnavailableReason();

            Assert.NotNull(reason);
            Assert.Contains("API key", reason!, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("OpenRouter", reason!, StringComparison.Ordinal);
        }
        finally
        {
            SettingsService.Update(s =>
            {
                s.EnableOnlineAiSafety = originalEnabled;
                s.AiProvider = originalProvider;
                s.SetAiApiKey("OpenRouter", originalKey);
            });
        }
    }

    [Fact]
    public void AiUnavailableReason_IsNullForAConfiguredProvider()
    {
        AppSettings saved = SettingsService.Current;
        bool originalEnabled = saved.EnableOnlineAiSafety;
        string originalProvider = saved.AiProvider;
        string originalKey = saved.GetAiApiKey("OpenRouter");

        try
        {
            SettingsService.Update(s =>
            {
                s.EnableOnlineAiSafety = true;
                s.AiProvider = "OpenRouter";
                s.SetAiApiKey("OpenRouter", "test-key");
            });

            Assert.Null(LargeFileHunterService.GetAiUnavailableReason());
        }
        finally
        {
            SettingsService.Update(s =>
            {
                s.EnableOnlineAiSafety = originalEnabled;
                s.AiProvider = originalProvider;
                s.SetAiApiKey("OpenRouter", originalKey);
            });
        }
    }

    [Fact]
    public void AiUnavailableReason_IsNullForTheBuiltInEngine()
    {
        AppSettings saved = SettingsService.Current;
        bool originalEnabled = saved.EnableOnlineAiSafety;
        string originalProvider = saved.AiProvider;

        try
        {
            // The built-in engine answers locally, so neither the master switch nor a key
            // can make an Ask-AI request impossible.
            SettingsService.Update(s =>
            {
                s.EnableOnlineAiSafety = false;
                s.AiProvider = "BuiltIn";
            });

            Assert.Null(LargeFileHunterService.GetAiUnavailableReason());
        }
        finally
        {
            SettingsService.Update(s =>
            {
                s.EnableOnlineAiSafety = originalEnabled;
                s.AiProvider = originalProvider;
            });
        }
    }

    [Fact]
    public void AiReadiness_ClassifiesEachObstacle()
    {
        AppSettings saved = SettingsService.Current;
        bool originalEnabled = saved.EnableOnlineAiSafety;
        string originalProvider = saved.AiProvider;
        string originalKey = saved.GetAiApiKey("OpenRouter");

        try
        {
            SettingsService.Update(s =>
            {
                s.EnableOnlineAiSafety = false;
                s.AiProvider = "OpenRouter";
                s.SetAiApiKey("OpenRouter", "test-key");
            });

            Assert.Equal(
                LargeFileHunterService.AiReadiness.SwitchOff,
                LargeFileHunterService.GetAiReadiness(out string? offReason));
            Assert.NotNull(offReason);

            SettingsService.Update(s =>
            {
                s.EnableOnlineAiSafety = true;
                s.AiProvider = "OpenRouter";
                s.SetAiApiKey("OpenRouter", string.Empty);
            });

            Assert.Equal(
                LargeFileHunterService.AiReadiness.MissingKey,
                LargeFileHunterService.GetAiReadiness(out string? keyReason));
            Assert.NotNull(keyReason);

            SettingsService.Update(s => s.SetAiApiKey("OpenRouter", "test-key"));

            Assert.Equal(
                LargeFileHunterService.AiReadiness.Ready,
                LargeFileHunterService.GetAiReadiness(out string? readyReason));
            Assert.Null(readyReason);

            // BuiltIn answers locally: neither the switch nor a key can block it.
            SettingsService.Update(s =>
            {
                s.EnableOnlineAiSafety = false;
                s.AiProvider = "BuiltIn";
            });

            Assert.Equal(
                LargeFileHunterService.AiReadiness.BuiltInLocal,
                LargeFileHunterService.GetAiReadiness(out string? builtInReason));
            Assert.Null(builtInReason);
        }
        finally
        {
            SettingsService.Update(s =>
            {
                s.EnableOnlineAiSafety = originalEnabled;
                s.AiProvider = originalProvider;
                s.SetAiApiKey("OpenRouter", originalKey);
            });
        }
    }

    [Fact]
    public void TryEnableConfiguredProvider_TurnsTheSwitchOnForAConfiguredProvider()
    {
        AppSettings saved = SettingsService.Current;
        bool originalEnabled = saved.EnableOnlineAiSafety;
        string originalProvider = saved.AiProvider;
        string originalKey = saved.GetAiApiKey("OpenRouter");

        try
        {
            SettingsService.Update(s =>
            {
                s.EnableOnlineAiSafety = false;
                s.AiProvider = "OpenRouter";
                s.SetAiApiKey("OpenRouter", "test-key");
            });

            Assert.True(LargeFileHunterService.TryEnableConfiguredProvider());
            Assert.True(SettingsService.Current.EnableOnlineAiSafety);
            Assert.Null(LargeFileHunterService.GetAiUnavailableReason());
        }
        finally
        {
            SettingsService.Update(s =>
            {
                s.EnableOnlineAiSafety = originalEnabled;
                s.AiProvider = originalProvider;
                s.SetAiApiKey("OpenRouter", originalKey);
            });
        }
    }

    [Fact]
    public void TryEnableConfiguredProvider_RefusesWithoutAKeyOrAProvider()
    {
        AppSettings saved = SettingsService.Current;
        bool originalEnabled = saved.EnableOnlineAiSafety;
        string originalProvider = saved.AiProvider;
        string originalKey = saved.GetAiApiKey("OpenRouter");

        try
        {
            // No key: there is nothing to call, so enabling the switch would only turn a
            // clear "add a key" message into a failed request.
            SettingsService.Update(s =>
            {
                s.EnableOnlineAiSafety = false;
                s.AiProvider = "OpenRouter";
                s.SetAiApiKey("OpenRouter", string.Empty);
            });

            Assert.False(LargeFileHunterService.TryEnableConfiguredProvider());
            Assert.False(SettingsService.Current.EnableOnlineAiSafety);

            // BuiltIn: local analysis already works; no switch to flip.
            SettingsService.Update(s =>
            {
                s.EnableOnlineAiSafety = false;
                s.AiProvider = "BuiltIn";
            });

            Assert.False(LargeFileHunterService.TryEnableConfiguredProvider());
            Assert.False(SettingsService.Current.EnableOnlineAiSafety);
        }
        finally
        {
            SettingsService.Update(s =>
            {
                s.EnableOnlineAiSafety = originalEnabled;
                s.AiProvider = originalProvider;
                s.SetAiApiKey("OpenRouter", originalKey);
            });
        }
    }

    [Fact]
    public void TryEnableConfiguredProvider_IsANoOpWhenAlreadyEnabled()
    {
        AppSettings saved = SettingsService.Current;
        bool originalEnabled = saved.EnableOnlineAiSafety;
        string originalProvider = saved.AiProvider;
        string originalKey = saved.GetAiApiKey("OpenRouter");

        try
        {
            SettingsService.Update(s =>
            {
                s.EnableOnlineAiSafety = true;
                s.AiProvider = "OpenRouter";
                s.SetAiApiKey("OpenRouter", "test-key");
            });

            Assert.True(LargeFileHunterService.TryEnableConfiguredProvider());
            Assert.True(SettingsService.Current.EnableOnlineAiSafety);
        }
        finally
        {
            SettingsService.Update(s =>
            {
                s.EnableOnlineAiSafety = originalEnabled;
                s.AiProvider = originalProvider;
                s.SetAiApiKey("OpenRouter", originalKey);
            });
        }
    }

    // -----------------------------------------------------------------
    // Picker: FREE marker, search filter, saved-model merge
    // -----------------------------------------------------------------

    [Fact]
    public void Catalog_IsFreeFollowsTheBadgeAndNothingElse()
    {
        // The pill's visibility binds to this, so it has to agree with the badge the
        // parser writes and stay false for anything that merely mentions "free".
        Assert.True(new AiModelOption("a", "A", Badge: "FREE").IsFree);
        Assert.True(new AiModelOption("b", "B", Badge: "FREE").IsFree);
        Assert.False(new AiModelOption("c", "C").IsFree);
        Assert.False(new AiModelOption("d", "D", Badge: "").IsFree);
        Assert.False(new AiModelOption("e", "E", Badge: "FREE TIER").IsFree);
        Assert.False(new AiModelOption("f", "F", Note: "free trial", Badge: null).IsFree);
    }

    [Fact]
    public void FriendlyName_HumanisesIdOnlyProvidersWithoutInventingNames()
    {
        // OpenAI and Groq answer /models with ids only. Before this, every row in their
        // pickers read as wire data ("gpt-5.4-mini", "openai/gpt-oss-120b"), which looked
        // broken next to the hand-written catalog names. A catalog entry always wins;
        // otherwise the id is humanised by rule so an unfamiliar model still has a label.
        var cases = new (string Provider, string Id, string Expected)[]
        {
            ("OpenAI", "gpt-5.4-mini", "GPT-5.4 mini"),
            ("OpenAI", "gpt-4o", "GPT-4o"),
            ("OpenAI", "o3-mini", "o3 mini"),
            ("Groq", "openai/gpt-oss-120b", "GPT-OSS 120B"),
            ("Groq", "llama-3.3-70b-versatile", "Llama 3.3 70B"), // curated catalog entry wins
            ("Groq", "llama-3.1-8b-instant", "Llama 3.1 8B Instant"), // not in the catalog, humanised
            ("Groq", "moonshotai/kimi-k2-instruct", "Kimi K2 Instruct"),
            ("Groq", "deepseek-r1-distill-llama-70b", "DeepSeek R1 Distill Llama 70B"),
            ("Groq", "gemma2-9b-it", "Gemma 2 9B IT"),
            ("Ollama", "llama3.2:latest", "Llama 3.2"),
            ("Ollama", "qwen2.5:7b", "Qwen2.5 7B"),
            ("OpenRouter", "google/gemma-4-31b-it:free", "Gemma 4 31B"),
            ("OpenRouter", "some/unknown-model-7b", "Unknown Model 7B"),
        };

        foreach ((string provider, string id, string expected) in cases)
        {
            Assert.Equal(expected, AiModelCatalog.GetFriendlyName(provider, id));
        }

        // The hint names the model the request will use, and still shows the id that is sent.
        Assert.Equal(
            "GPT-5.4 mini (gpt-5.4-mini)",
            AiModelCatalog.DescribeChosenModel("OpenAI", "gpt-5.4-mini"));
        Assert.Equal(
            "Unknown Model 7B (some/unknown-model-7b)",
            AiModelCatalog.DescribeChosenModel("OpenRouter", "some/unknown-model-7b"));
    }

    [Fact]
    public void WithSavedModel_KeepsAnIdTheProviderDidNotReturn()
    {
        // Without this the picker would look like it forgot the user's choice, and the
        // next Save would write an empty model id.
        AiModelOption[] models =
        [
            new("google/gemma-4-31b-it:free", "Gemma 4 31B", Badge: "FREE"),
        ];

        IReadOnlyList<AiModelOption> merged =
            AiModelCatalog.WithSavedModel(models, "my-private-fine-tune", "OpenAI");

        Assert.Equal(2, merged.Count);
        Assert.Equal("my-private-fine-tune", merged[0].Id);
        // The saved id keeps its row and reads as a name, not as the raw identifier.
        Assert.Equal("My Private Fine Tune", merged[0].DisplayName);
        Assert.Contains("Saved in settings", merged[0].Note!, StringComparison.Ordinal);
        Assert.False(merged[0].IsFree);
        Assert.Equal("google/gemma-4-31b-it:free", merged[1].Id);
    }

    [Fact]
    public void WithSavedModel_DoesNotDuplicateAnIdTheListAlreadyHas()
    {
        AiModelOption[] models =
        [
            new("model-a", "Model A"),
            new("model-b", "Model B"),
        ];

        // Case-insensitive: a settings file that saved MODEL-A is the same model.
        Assert.Same(models, AiModelCatalog.WithSavedModel(models, "MODEL-A"));
        Assert.Same(models, AiModelCatalog.WithSavedModel(models, "model-a"));
    }

    [Fact]
    public void WithSavedModel_BlankSavedIdReturnsTheSameListInstance()
    {
        AiModelOption[] models = [new("model-a", "Model A")];

        Assert.Same(models, AiModelCatalog.WithSavedModel(models, null));
        Assert.Same(models, AiModelCatalog.WithSavedModel(models, "   "));
    }

    [Fact]
    public void FreeModelsOnly_HidesPaidRowsWhenTheProviderPublishesPricing()
    {
        AiModelOption[] models =
        [
            new("google/gemma-4-31b-it:free", "Gemma 4 31B", "No credit needed", "FREE"),
            new("meta-llama/llama-3.3-70b-instruct", "Llama 3.3 70B", "$0.59/M in"),
            new("xingchen-agi/xing4.0-29b-a4b:free", "Xing4.0 29B A4B", "No credit needed", "FREE"),
        ];

        IReadOnlyList<AiModelOption> free = AiModelCatalog.FreeModelsOnly(models);

        Assert.Equal(2, free.Count);
        Assert.All(free, model => Assert.True(model.IsFree));
        Assert.Equal(models[0].Id, free[0].Id);
        Assert.Equal(models[2].Id, free[1].Id);
    }

    [Fact]
    public void FreeModelsOnly_KeepsTheWholeListWhenNoPricingIsPublished()
    {
        // OpenAI, Groq, Gemini and Ollama answer with ids and no prices. Treating "no FREE
        // badge" as "paid" would empty those pickers and leave nothing to choose.
        AiModelOption[] models =
        [
            new("gpt-5.4-mini", "GPT-5.4 mini"),
            new("gpt-5.4-nano", "GPT-5.4 nano"),
        ];

        Assert.Same(models, AiModelCatalog.FreeModelsOnly(models));
    }

    [Fact]
    public void IsSameModelSet_ComparesByIdNameNoteAndBadge()
    {
        AiModelOption[] first =
        [
            new("google/gemma-4-31b-it:free", "Gemma 4 31B", "No credit needed", "FREE"),
            new("meta-llama/llama-3.3-70b-instruct", "Llama 3.3 70B", "$0.59/M in"),
        ];

        // A refresh always builds a new list; equal values must not rebuild the picker.
        Assert.True(AiModelCatalog.IsSameModelSet(first, first.ToArray()));

        // A pricing change reaches the picker even when the ids are unchanged.
        AiModelOption[] repriced =
        [
            first[0],
            new("meta-llama/llama-3.3-70b-instruct", "Llama 3.3 70B", "$0.49/M in"),
        ];
        Assert.False(AiModelCatalog.IsSameModelSet(first, repriced));

        // So does a row appearing or disappearing.
        Assert.False(AiModelCatalog.IsSameModelSet(first, [first[0]]));
        Assert.False(AiModelCatalog.IsSameModelSet(first, null));
        Assert.False(AiModelCatalog.IsSameModelSet(null, first));
        Assert.True(AiModelCatalog.IsSameModelSet(null, null));
    }

    [Fact]
    public void Validator_ModelWithASpacePointsAtThePicker()
    {
        // The free-text box is gone, so the warning must not tell the user to clear a
        // control that no longer exists.
        IReadOnlyList<AiSettingsIssue> issues = AiSettingsValidator.Validate(
            "OpenAI", apiKey: "test-key", ollamaEndpoint: null, modelName: "gpt 5.4");

        AiSettingsIssue issue = Assert.Single(issues);
        Assert.Contains("Pick a model from the list", issue.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Clear the box", issue.Message, StringComparison.Ordinal);
    }
}
