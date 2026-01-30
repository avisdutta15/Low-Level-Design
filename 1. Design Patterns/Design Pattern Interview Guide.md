## Design Patterns Interview Tips: 

**Efficiently utilizing design patterns in an LLD interview isn't about memorizing them—it's about recognizing the nature of the problem.**

Here is a step-by-step framework to guide you from problem statement to pattern selection.

----

**Step 1: The "Why" Before the "How" (Problem Identification)**

Don't start by saying, "I'll use the Strategy Pattern." Start by identifying the **pain point** or **requirement** that hints at a pattern.
- **Is there complex object creation?** -> Think Creational (Factory, Builder, Singleton).
- **Are there multiple ways to do the same thing?** -> Think Behavioral (Strategy).
- **Do objects need to talk without tight coupling?** -> Think Behavioral (Observer, Mediator). 
- **Do you need to add functionality to an object dynamically?** -> Think Structural (Decorator).

Example:

- *Interviewer*: "Design a Notification Service that sends alerts via Email, SMS, and Push.
- *You*: "Since we have multiple algorithms for sending a message (Email/SMS/Push) and the client shouldn't care how it's sent, this screams Strategy Pattern."

----

**Step 2: The "MVP" (Minimum Viable Pattern)**
Start simple. Don't over-engineer. Use the pattern to solve the immediate problem, not a hypothetical future one.

**Scenario:** Design a **Parking Lot System**.

- **Requirement:** We have different types of spots (Compact, Large, Handicapped) and different vehicles.
- **Pattern Selection:**
    -   **Factory Pattern:** To create the right `ParkingSpot` object based on the `VehicleType`.
    
    - **Singleton**: For the `ParkingLot` manager itself (there's only one lot).
    
**Code Thought Process:**
- *Bad:* "I'll make an AbstractFactory for every vehicle part." (Too complex).
- *Good:* "I'll use a simple Factory Method GetSpot(VehicleType type) to return the correct spot instance."

----

**Step 3: Justify Your Choice (The "Because" Clause)**

This is crucial. You must articulate why you chose a pattern.

- *Wrong:* "I used the Observer pattern."
- *Right:* "I used the Observer pattern because the Display Board needs to update automatically whenever a car enters or leaves. If we just polled the database every second, it would be inefficient. The 'Event' model allows the Board to react only when state changes."

----

**Step 4: Refine and Scale (Adding Patterns as Needed)**
As the interview progresses, the interviewer will add complexity. This is your cue to layer in more patterns.

**Scenario Expansion:** "Now, we need to calculate fees. Weekends are different from weekdays. VIPs get discounts."

- **Problem:** Calculating fees is getting messy with `if-else` blocks.
- **Pattern: Strategy Pattern.** 
    - Create an interface `IFeeStrategy`.
    - Implement `WeekendStrategy`, `WeekdayStrategy`, `VipStrategy`.
    - Inject the right strategy into the `Ticket` object.

----

**Step 5: Handling Cross-Cutting Concerns (The Polish)**

Finally, address system-wide issues like logging, validation, or legacy code integration.
- **Problem:** "We need to log every time a gate opens."
- **Pattern:** Proxy or Decorator. Wrap the Gate object with a LoggingGateProxy that logs the action and then calls the real gate.
- **Problem:** "We have an old legacy camera system that uses a weird API."
- **Pattern:** Adapter. Create a wrapper that makes the old API look like your new, clean interface.


--------

This table maps specific "Trigger Words" (hints the interviewer gives) to the correct Design Pattern. If you hear the phrase in the left column, the answer is likely the pattern in the right column.

**1. Creational Patterns (Object Construction)**

*Focus: Managing the complexity of creating objects.*

|If you hear... |The Pattern is... | Why? |
|---------------|------------------|------|
|"We need exactly one instance of this globally..."| **Singleton** | Ensures a single shared instance (e.g., DB Config, Logger).|
|"The object has 10+ parameters or optional fields..."| **Builder** | Cleans up complex constructors; step-by-step creation.|
|"We don't know which specific class to create until runtime..."| **Factory Method** | Delays the decision of which class to instantiate to subclasses.|
|"We need families of related objects (e.g., Windows Buttons vs. Mac Buttons)..."| **Abstract Factory** | Creates groups of related objects without specifying concrete classes.|


**2. Structural Patterns (System Assembly)**

*Focus: How objects connect and form larger structures.*

|If you hear...|The Pattern is...|Why?|
|--------------|-----------------|----|
|"We need to use an old class that doesn't fit our new interface..."| **Adapter** | Translates one interface to another (like a travel plug).|
|"We need to add features (logging, borders) dynamically without inheritance..."|**Decorator**|Wraps an object to add behavior at runtime.|
|"We have a Tree Structure or Hierarchy (Folders/Files)..."| **Composite**| Treats individual objects and groups of objects uniformly.|
|"This operation is expensive, so we need to cache or lazy-load it..."|	**Proxy** |	Acts as a gatekeeper; controls access to the real object.|
|"We need to hide a complex subsystem behind a simple interface..."|	**Facade**	| Provides a simplified entry point (e.g., Car.Start() handles the engine, fuel, battery logic internally).|

**3. Behavioral Patterns (Object Communication)**

*Focus: How objects talk to each other and assign responsibilities.*

|If you hear...|The Pattern is...|Why?|
|--------------|-----------------|----|
|"We have multiple ways to do X (e.g., Payment Methods, Sorting Algorithms)..."	| **Strategy**	| Encapsulates interchangeable algorithms.|
|"When X changes, we need to notify Y, Z, and A..."	|**Observer**|	Creates a subscription mechanism (Events).|
|"We need Undo/Redo functionality..."	|**Command**|	Encapsulates a request as an object (store history).|
|"The object acts differently based on its status (Idle vs. Running)..."	| **State**	| Allows an object to alter its behavior when its internal state changes.|
|"We have a sequence of checks or a processing pipeline..."	|**Chain of Responsibility** |	Passes a request along a chain of handlers.|
|"We have many objects talking to each other, creating a messy web..."	| **Mediator** |	Centralizes communication (like an Air Traffic Controller).|