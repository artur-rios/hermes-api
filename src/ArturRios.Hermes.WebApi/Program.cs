namespace ArturRios.Hermes.WebApi;

public class Program
{
    public static void Main(string[] args) => CreateApplication(CreateBuilder(args)).Run();

    public static WebApplicationBuilder CreateBuilder(string[] args) => WebApplication.CreateBuilder(args);

    public static WebApplication CreateApplication(WebApplicationBuilder builder) => builder.Build();
}
