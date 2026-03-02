using System;

namespace UFOLEP84.App.Services;

public static class ServiceLocator
{
    public static T Get<T>() where T : notnull
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        if (services is null)
            throw new InvalidOperationException("Le conteneur de services MAUI est indisponible.");

        var service = services.GetService(typeof(T));
        if (service is T typed)
            return typed;

        throw new InvalidOperationException($"Service non enregistré: {typeof(T).FullName}");
    }
}
