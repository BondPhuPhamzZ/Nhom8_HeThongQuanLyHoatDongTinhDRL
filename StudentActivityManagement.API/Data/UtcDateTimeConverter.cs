using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StudentActivityManagement.API.Serialization;

namespace StudentActivityManagement.API.Data
{
    public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter()
            : base(value => UtcDateTimeJsonConverter.ToUtc(value), value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
        {
        }
    }

    public sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
    {
        public NullableUtcDateTimeConverter()
            : base(
                value => value.HasValue ? UtcDateTimeJsonConverter.ToUtc(value.Value) : value,
                value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value)
        {
        }
    }
}
