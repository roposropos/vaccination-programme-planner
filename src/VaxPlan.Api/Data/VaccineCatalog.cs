using VaxPlan.Api.Domain;

namespace VaxPlan.Api.Data;

// Simplified schedule based on the Polish immunisation programme (PSO). Demo data, not for clinical use.
public static class VaccineCatalog
{
    public static List<Vaccine> Create() =>
    [
        Define("HBV", "WZW typu B", "Wirusowe zapalenie wątroby typu B", "08", "Z24.6", true,
            (1, 0, "w ciągu 24 h po urodzeniu"), (2, 2, "2. miesiąc życia"), (3, 7, "7. miesiąc życia")),
        Define("BCG", "BCG", "Gruźlica", "19", "Z23.2", true,
            (1, 0, "przed wypisem z oddziału noworodkowego")),
        Define("ROTA", "Rotawirusy", "Zakażenia rotawirusowe", "122", "Z25.8", true,
            (1, 2, "2. miesiąc życia"), (2, 3, "3. miesiąc życia")),
        Define("DTAP", "DTaP", "Błonica, tężec, krztusiec", "20", "Z27.1", true,
            (1, 2, "2. miesiąc życia"), (2, 4, "4. miesiąc życia"), (3, 16, "16.–18. miesiąc życia"), (4, 72, "6. rok życia")),
        Define("IPV", "IPV", "Poliomyelitis", "10", "Z24.0", true,
            (1, 2, "2. miesiąc życia"), (2, 4, "4. miesiąc życia"), (3, 16, "16.–18. miesiąc życia"), (4, 72, "6. rok życia")),
        Define("HIB", "Hib", "Haemophilus influenzae typu b", "17", "Z23.8", true,
            (1, 2, "2. miesiąc życia"), (2, 4, "4. miesiąc życia"), (3, 16, "16.–18. miesiąc życia")),
        Define("PCV", "Pneumokoki (PCV13)", "Inwazyjna choroba pneumokokowa", "133", "Z23.8", true,
            (1, 2, "2. miesiąc życia"), (2, 4, "4. miesiąc życia"), (3, 13, "13.–15. miesiąc życia")),
        Define("MMR", "MMR", "Odra, świnka, różyczka", "03", "Z27.4", true,
            (1, 13, "13.–15. miesiąc życia"), (2, 72, "6. rok życia")),
        Define("TDAP", "Tdap (przypominająca)", "Błonica, tężec, krztusiec", "115", "Z27.1", true,
            (1, 168, "14. rok życia")),
        Define("HPV", "HPV (9-walentna)", "Zakażenia wirusem brodawczaka ludzkiego", "165", "Z25.8", false,
            (1, 144, "12. rok życia, szczepienie zalecane"))
    ];

    private static Vaccine Define(
        string code, string name, string disease, string cvx, string icd10, bool mandatory,
        params (int Dose, int AgeInMonths, string Description)[] doses) =>
        new()
        {
            Code = code,
            Name = name,
            TargetDisease = disease,
            CvxCode = cvx,
            Icd10Code = icd10,
            Mandatory = mandatory,
            Doses = doses
                .Select(d => new ScheduleDose { DoseNumber = d.Dose, AgeInMonths = d.AgeInMonths, Description = d.Description })
                .ToList()
        };
}
