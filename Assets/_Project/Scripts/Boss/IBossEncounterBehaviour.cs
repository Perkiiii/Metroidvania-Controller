using System;

public interface IBossEncounterBehaviour
{
    event Action IntroCompleted;
    event Action DefeatPresentationCompleted;

    bool TryPrepareForEncounter();
    void PlayIntro();
    void BeginCombat();
    void InterruptEncounter();
    void NotifyEncounterCompleted();
}
