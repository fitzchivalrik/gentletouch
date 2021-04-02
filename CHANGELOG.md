# CHANGELOG

## Unreleased

- fix: Canceling the initial warning does _not_ lock one out permanently anymore
- fix: Cooldown triggers are now properly reset when leaving combat
- feature(UI): Add padding for nice alignment
- feature(UI): Cooldown triggers are now separated into jobs, no more single list for all.
- feature(AetherCurrent): Aether currents can now be sensed via vibration when out-of-combat. Toggleable.
  
**BREAKING**:

- Due to triggers' separation into jobs, the 'All Job GCD' trigger was lost.
  Existing user will automatically get a new GCD trigger for each Job.  
  New Users will have a choice during new onboarding process.

## 0.3.0 (testing)

- Initial Release
- Create different vibration patterns
- Add cooldown triggers to perform patterns when its safe to activate the cooldown again