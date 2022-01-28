﻿
        public class EfCoreValueConverter : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<VOTYPE, System.DateTimeOffset>
        {
            public EfCoreValueConverter(Microsoft.EntityFrameworkCore.Storage.ValueConversion.ConverterMappingHints mappingHints = null)
                : base(
                    vo => vo.Value,
                    value => new VOTYPE(value),
                    mappingHints
                ) { }
        }