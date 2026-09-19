using AutoMapper;
using Library.Service.Mapping;
using Microsoft.Extensions.Logging.Abstractions;

namespace Library.Tests.Services;

internal static class TestHelpers
{
    /// <summary>The real production mapping profile, so tests also catch mapping mistakes.</summary>
    public static IMapper CreateMapper() =>
        new MapperConfiguration(c => c.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
}
