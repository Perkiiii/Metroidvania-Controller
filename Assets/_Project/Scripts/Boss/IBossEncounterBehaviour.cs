using System;

public interface IBossEncounterBehaviour
{
    event Action IntroCompleted;
    event Action DefeatPresentationCompleted;

    void PrepareForEncounter();
    void PlayIntro();
    void BeginCombat();
    void InterruptEncounter();
    void NotifyEncounterCompleted();
}
