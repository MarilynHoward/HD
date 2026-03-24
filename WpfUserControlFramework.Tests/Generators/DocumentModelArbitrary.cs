using FsCheck;
using RestaurantPosWpf;

namespace RestaurantPosWpf.Tests.Generators
{
    /// <summary>
    /// Custom FsCheck Arbitrary for <see cref="DocumentModel"/>.
    /// Register via <c>Arb.Register&lt;DocumentModelArbitrary&gt;()</c> or use
    /// <c>[Arbitrary(typeof(DocumentModelArbitrary))]</c> on property tests.
    /// </summary>
    public class DocumentModelArbitrary
    {
        private static readonly string[] FileTypes = { "PDF", "DOCX", "XLSX", "TXT" };

        public static Arbitrary<DocumentModel> DocumentModel()
        {
            var gen =
                from name in Arb.Generate<NonNull<string>>()
                from category in Gen.Elements(DocumentCategories.All.ToArray())
                from fileType in Gen.Elements(FileTypes)
                from fileSize in Gen.Choose(0, int.MaxValue).Select(n => (long)n)
                from year in Gen.Choose(2000, 2026)
                from month in Gen.Choose(1, 12)
                from day in Gen.Choose(1, 28) // safe for all months
                from notes in Arb.Generate<NonNull<string>>()
                from fileName in Arb.Generate<NonNull<string>>()
                select new RestaurantPosWpf.DocumentModel
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name.Get,
                    Category = category,
                    FileType = fileType,
                    FileSize = fileSize,
                    UploadDate = new DateTime(year, month, day),
                    Notes = notes.Get,
                    FileName = fileName.Get
                };

            return gen.ToArbitrary();
        }
    }
}
