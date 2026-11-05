namespace MeridianLogistics.Api.Llm;

public static class LlmServiceCollectionExtensions
{
    /// <summary>
    /// Registers the concrete provider selected by "Llm:Provider", then exposes
    /// <see cref="ILlmProvider"/> as the <see cref="TracedLlmProvider"/> decorator
    /// wrapping it. Callers only ever see ILlmProvider.
    /// </summary>
    public static IServiceCollection AddLlmProvider(this IServiceCollection services, IConfiguration config)
    {
        var provider = config[$"{LlmOptions.SectionName}:Provider"]?.ToLowerInvariant();

        if (provider == "anthropic")
            services.AddHttpClient<AnthropicLlmProvider>();
        else
            services.AddHttpClient<OpenAiCompatibleLlmProvider>();

        services.AddSingleton<ILlmProvider>(sp =>
        {
            ILlmProvider concrete = provider == "anthropic"
                ? sp.GetRequiredService<AnthropicLlmProvider>()
                : sp.GetRequiredService<OpenAiCompatibleLlmProvider>();
            return new TracedLlmProvider(concrete);
        });

        return services;
    }
}
