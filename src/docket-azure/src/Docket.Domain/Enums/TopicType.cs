namespace Docket.Domain.Enums;

public enum TopicType
{
    Adhoc,     // Exists only in the Minutes it was created on
    Recurring  // Carries forward if IsOpen at the time the next Minutes is created
}
