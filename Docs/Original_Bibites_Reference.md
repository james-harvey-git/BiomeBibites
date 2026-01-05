# The Bibites Simulation – Overview and Mechanics

## Overview and Philosophy

* **The Bibites** is an artificial‑life simulation created by Leo Caussan.  Bibites are small digital organisms living in a 2‑D world with evolving genetics and neural‑network brains.  Each bibite starts with a simple genome and an empty brain; random mutations connect nodes and create behaviours, enabling the organism to harvest energy, survive and reproduce.  The simulation implements reproduction, mutation and natural selection, letting complex behaviours emerge over long runs【332528119055686†L20-L34】.  Planned future developments include seasonal cycles, sexual reproduction and module‑based evolutionary systems【332528119055686†L72-L80】.

* The simulation is interactive.  Users can throw bibites or pellets around, selectively feed or kill bibites, force them to lay eggs, adjust parameters mid‑simulation and save customised scenarios【332528119055686†L63-L70】.  Engineering tools allow editing bibite genomes/brains and creating scenarios; challenges test the user’s genetic engineering skills【332528119055686†L54-L57】.

* Energy is conserved; the simulation is a closed system.  Energy flows between biomass, plant pellets, meat pellets, eggs, bibite bodies and reserves.  Actions cost energy; inefficiencies return energy to the environment as biomass【941281487284828†L144-L150】.  Biomass is counted in energy units (E); it represents energy available to spawn new plant pellets and bibites【332578895908581†L140-L160】.

## Entities and Energy Sources

| Entity                  | Purpose/Description                                                                    | Evidence |
|-------------------------|----------------------------------------------------------------------------------------|---------|
| **Bibites**            | Digital organisms.  Have genes controlling body colour, size, diet, metabolism, strength, immune activation, growth, organ sizes (WAGG genes) and behavioural weights.  They evolve via mutation and natural selection. | Gene list summarised from the wiki【99380404264405†L197-L234】. |
| **Plant pellets**      | Main food items.  Spawn by consuming biomass and recycle energy to a usable form.  Their density is controlled by *Biomass Density*【640085546704444†L146-L149】. | 【640085546704444†L146-L149】 |
| **Meat pellets**       | Produced when bibites die; eventually rot, returning energy to biomass【332578895908581†L148-L156】 (implied by biomass sources). | 【332578895908581†L148-L156】 |
| **Pheromones**         | Chemical cues (red, green and blue channels) emitted by bibites.  Detected by specific input neurons and used for social communication or trickery【141576286349584†L142-L157】. | 【141576286349584†L142-L157】 |
| **Viruses (optional)** | When the **virus‑enabled** setting is on, viruses (virions) spawn at a rate controlled by the *Virus Generation Time*.  Viruses can infect bibites【879210222847848†L142-L151】. | 【879210222847848†L142-L151】 |

## Genes and Mutation

* **Genes** control almost every physical and behavioural trait in a bibite.  At reproduction, each gene value is copied to the offspring and then mutated according to the mutation rules described below.  The key genes include:

  * **Red Colour, Green Colour and Blue Colour** – specify the amount of red, green and blue pigments in the bibite’s skin, determining its body colour.  The visual appearance of a bibite is procedurally generated from these values.
  * **Eye Hue Offset** – shifts the hue of the bibite’s eyes relative to the body colour.
  * **Diet** – determines which type of food is best suited to the bibite: `0` means completely herbivorous, `1` means completely carnivorous, and intermediate values support omnivory depending on food parameters.
  * **Size Ratio** – the one‑dimensional size scaling factor for the bibite.  Larger sizes increase metabolism (energy cost per second) and influence many other dynamics.
  * **Metabolism Speed** – controls the relative metabolic activity of the bibite.  Higher values make movement, digestion and other processes faster but increase the baseline energy cost of living.
  * **Average Gene Mutations** – sets the expected number of gene mutations applied to the genome at reproduction.
  * **Gene Mutation Variance** – the standard deviation of the magnitude of gene mutation changes.  Expressed as a fraction of the total gene range (plus a relative component), this value determines how large mutations can be.
  * **Average Brain Mutations** – sets the expected number of brain mutation events (synapse weight changes, node additions/removals, etc.) per reproduction.
  * **Brain Mutation Variance** – the standard deviation of the amount of change in brain mutations (measured relative to connection weight ranges).
  * **Lay Time** – the default time it takes for a bibite to produce an egg when the egg‑production node is fully activated.  This duration scales inversely with the metabolism speed: slower metabolisms double the lay time, faster metabolisms halve it.
  * **Brood Time** – used in the formula `(hatchTime/broodTime)^2` that determines the bibite’s maturity at birth.  Higher brood times cause bibites to be born smaller; it has little effect on adults beyond this calculation.
  * **Hatch Time** – how long an egg takes to hatch.  Longer hatch times usually cost more energy but result in better‑developed newborns with higher survival chances.
  * **View Radius** – the maximum distance a bibite can see for its vision sensors.
  * **View Angle** – the field of view (in radians) for the vision system.  Narrow fields concentrate on what’s ahead; wide fields can see threats or food behind the bibite.
  * **Clock Period** – the period of the internal clock in the brain.  This controls oscillatory senses such as the Tic counter and influences time‑related behaviours.
  * **Pheromones Sensing Radius** – determines how far a bibite can sense pheromone trails and how strongly it responds to them; larger radii allow detecting pheromones from farther away but can also cause sensory overload.

  **Herding and social behaviour genes**

  * **Herd Separation Weight** – controls how strongly the separation rule is applied when herding.  Higher values make bibites maintain personal space and avoid collisions.
  * **Herd Alignment Weight** – controls the alignment rule.  When large, bibites tend to match their heading with their neighbours so the group travels in the same direction.
  * **Herd Cohesion Weight** – controls the cohesion rule that pulls bibites towards the centre of their herd to keep the group together.
  * **Herd Velocity Weight** – determines how strongly the velocity rule is applied; high values cause a bibite to match its speed to that of its neighbours.
  * **Herd Separation Distance** – the distance bibites try to keep between themselves when moving as a herd.  Typical adult bibites measure ~10 units long; separation distances near this value produce tight groups whereas larger distances spread the herd.

  **Growth control genes**

  * **Growth Scale Factor** – scales the overall size and height of the growth curve.  Higher values yield larger adults at the same size ratio.
  * **Growth Maturity Factor** – controls how strongly maturity influences growth rate.  Large factors cause growth to accelerate rapidly as maturity increases; small factors produce more linear growth.
  * **Growth Maturity Exponent** – sets the exponent in the maturity–growth relationship.  Exponents >1 create sigmoidal curves (slow early growth, fast later growth) and exponents <1 invert this behaviour.

  **Organ size genes (Weighted Apportionment Genes – WAG)**

  * **Arm Muscles WAG** – determines the share of internal area dedicated to arm muscles.  Larger arm muscles increase force output and speed.
  * **Stomach WAG** – determines the share of internal area dedicated to the stomach.  Larger stomachs can digest more food simultaneously, increasing energy intake.
  * **Egg Organ WAG** – controls the size of the reproductive organ that produces and stores eggs.  Larger egg organs allow larger clutches or more mature offspring.
  * **Fat Organ WAG** – controls the size of the fat storage organ.  Larger fat organs increase long‑term energy reserves.
  * **Armor WAG** – determines the amount of internal area devoted to armour.  Thick armour provides damage protection at the cost of other organs.
  * **Throat WAG** – controls throat size.  Larger throats allow biting bigger chunks and swallowing larger pellets but also increase bite and swallow duration.
  * **Jaw Muscles WAG** – determines the size of jaw muscles.  Bigger jaw muscles allow exerting more force when biting, increasing damage and bite size.

  **Fat metabolism genes**

  * **Fat Storage Threshold** – the internal energy ratio considered neutral for deciding whether to store or retrieve fat.  When energy exceeds this threshold, excess is converted to fat; when energy drops below it, fat is burned.
  * **Fat Storage Deadband** – defines a range around the storage threshold where fat storage or retrieval is inactive.  Within this band the bibite neither stores nor burns fat, preventing oscillations.

  These genes collectively define a bibite’s physiology and behaviour.  They interact with the brain: the neural network can respond to internal signals (e.g., low energy ratio, high pheromone intensity) and take actions (e.g., accelerate, bite, grow) that depend on gene values.  For example, a high Metabolism Speed gene makes movement faster but raises energy costs; a large Jaw Muscles WAG allows strong bites but reduces space for other organs.

* **Mutation process:** when a new egg is laid the offspring inherits the parent’s genes with mutations.  The *Mutation Chance* gene controls how many mutation events occur; the number of events follows a Poisson distribution【99380404264405†L150-L161】.  For each event a random gene is selected and its value modified.  The *Mutation Variance* gene determines the magnitude of change; relative change is drawn from a Gaussian distribution to ensure unbiased variations【99380404264405†L162-L176】.  An additional small absolute change is added to avoid genes getting stuck at zero【99380404264405†L179-L183】.

## Brain Architecture

* Bibites’ brains are recurrent neural networks inspired by **rt‑NEAT**【606131508599152†L144-L150】.  Brains consist of **nodes** and **synapses**.  Nodes represent neurons; they hold activation levels and can stimulate other nodes through synapses【919074648297762†L140-L154】.  There are three node types:
  * **Input nodes:** sensors for the bibite’s internal state or environment【919074648297762†L146-L148】.
  * **Output nodes:** actions that control movement and internal processes【919074648297762†L149-L151】.
  * **Hidden nodes:** intermediate neurons used to process signals【919074648297762†L152-L154】.

* Each node has an activation function (sigmoid, linear, tanh, sine, ReLU, etc.) and accumulates stimuli from incoming synapses【919074648297762†L160-L175】.  Synapses have strengths and can be enabled or disabled; disabled synapses can mutate back to enabled【696278303599722†L140-L151】.

* **Brain mutations:** when a new bibite is born, brain mutations can occur: changing synapse strength or sign, toggling synapses on/off, adding or removing synapses, changing a neuron’s activation function, or adding/removing neurons【606131508599152†L175-L194】.  The probability of each event is controlled by simulation settings【606131508599152†L196-L204】.

### Input neurons

The simulation defines many input neurons representing sensory channels (index shown).  Each input provides a continuous value to the brain【354801190563701†L140-L173】:

| Input neuron                     | Description | Index |
|----------------------------------|-------------|------|
| **EnergyRatio**                  | Current energy divided by maximum energy | 0 |
| **Maturity**                     | Maturity level (0→adult) | 1 |
| **LifeRatio**                    | Health divided by maximum health | 2 |
| **Fullness**                     | How full the stomach is | 3 |
| **Speed**                        | Current forward speed of the bibite (positive when moving forward; negative when moving backward) | 4 |
| **RotationSpeed**                | Current angular speed (normalised to full rotations per second).  Positive values mean clockwise rotation, negative values mean counterclockwise | – |
| **IsGrabbing**                   | 1 when grabbing, else 0 | 5 |
| **AttackedDamage**               | Damage taken this frame | 6 |
| **EggStored**                    | 1 if an egg is ready to lay, else 0 | 7 |
| **BibiteCloseness**              | Distance to nearest bibite | 8 |
| **BibiteAngle**                  | Average angle to visible bibites | 9 |
| **NBibites**                     | Number of visible bibites | 10 |
| **PlantCloseness**               | Distance to nearest plant pellet | 11 |
| **PlantAngle**                   | Average angle to plant pellets | 12 |
| **NPlants**                      | Number of visible plants / 4 | 13 |
| **MeatCloseness**                | Distance to nearest meat pellet | 14 |
| **MeatAngle**                    | Average angle to meat pellets | 15 |
| **NMeats**                       | Number of visible meats / 4 | 16 |
| **RedBibite**, **GreenBibite**, **BlueBibite** | Colour genes of the closest visible bibite | 17–19 |
| **Tic**                          | Rapid clock toggling between 1 and 0 | 20 |
| **Minute**                       | A minute counter (0–60) | 21 |
| **TimeAlive**                    | How long the bibite has been alive | 22 |
| **Phero1Angle**, **Phero2Angle**, **Phero3Angle** | Angle towards red/green/blue pheromones | 23–25 |
| **Phero1Heading**, **Phero2Heading**, **Phero3Heading** | Heading of pheromone trails | 26–28 |
| **PheroSense1**, **PheroSense2**, **PheroSense3** | Sense intensity for each pheromone colour | 29–31 |

### Output neurons

Output neurons drive actions; higher activation values lead to stronger actions.  The table below summarises every output in the current simulation and its behaviour.  For each output neuron we list what it controls, the effective output range (after applying the activation function), its activation function and the default activation level used when no connections stimulate it.

| Output neuron | Description | Output range | Activation function (default activation) |
|---|---|---|---|
| **Accelerate** | Controls forward/backward acceleration.  Positive values push the bibite forward; negative values push it backward (with a penalty) | –1 to 1 | TanH (default ≈0.45) |
| **Rotate** | Controls how strongly the bibite turns.  Negative activation applies torque to turn left; positive activation turns clockwise | –1 to 1 | TanH (default 0) |
| **Herding** | Gradually overrides the default movement behaviour to follow or avoid nearby bibites based on herding genes.  A value of 1.0 completely replaces movement with herding; negative values replace it with the opposite of what the herding genes dictate | –1 to 1 | TanH (default 0) |
| **EggProduction** | Controls how active the egg organ is.  Positive values (up to 1.0) produce eggs at the rate given by the *LayTime* gene; values between –0.15 and 0.15 make no change; negative values reabsorb stored eggs (with an energy penalty) | –1 to 1 | TanH (default 0.2) |
| **Want2Lay** | Determines whether the bibite will lay its stored eggs.  Activations below 0.25 do nothing; activations ≥ 0.25 cause stored eggs to be laid | 0 to 1 | Sigmoid (default 0) |
| **Want2Eat** | Controls the bibite’s mouth opening and whether it wants to swallow objects that can fit through the opening.  Positive values swallow; negative values cause vomiting; absolute values below 0.15 result in no swallowing/vomiting | –1 to 1 | TanH (default ≈1.23) |
| **Digestion** | Controls stomach acid level and digestion speed.  Faster digestion produces energy more quickly but is less efficient | 0 to 1 | Sigmoid (default ≈–2.07) |
| **Grab** | Controls grab strength.  When activated, any object entering the mouth will be grabbed.  Sudden negative activation causes grabbed objects to be thrown instead.  Force is distributed across all held or thrown objects.  Absolute activation < 0.15 results in no grabbing or throwing | –1 to 1 | TanH (default 0) |
| **Want2Attack** | Controls bite strength.  If activated, the bibite will try to bite objects in its mouth or newly entering it.  When already biting, continued activation drains/sucks the target (like a mosquito).  Activations below 0.15 cause no biting | 0 to 1 | Sigmoid (default 0) |
| **Want2Grow** | Controls growth speed.  The activation multiplies the bibite’s growth curve: 0 means no growth; 1.0 follows the natural growth curve determined by the growth genes | 0 to 1 | Sigmoid (default 0) |
| **ClkReset** | Resets the internal minute counter if activation exceeds 0.75 | 0 to 1 | Sigmoid (default 0) |
| **PhereOut1** | Controls red‑pheromone production.  An activation of 1.0 produces a pheromone spot lasting about 10 seconds; higher activations extend the duration | 0 to ∞ | ReLu (default 0) |
| **PhereOut2** | Controls green‑pheromone production.  Behaviour as for PhereOut1 | 0 to ∞ | ReLu (default 0) |
| **PhereOut3** | Controls blue‑pheromone production.  Behaviour as for PhereOut1 | 0 to ∞ | ReLu (default 0) |
| **Want2Heal** | Controls healing rate.  Positive values invest energy into recovering health; values below a small threshold do nothing | 0 to 1 | Sigmoid (default 0) |

**Note:** In the current simulation the activation functions of **output** neurons are **fixed**.  Mutations can change synaptic weights and toggle synapses on or off, but the activation function for each output node does not mutate.

### Hidden node activation functions

Hidden (intermediate) neurons can use a variety of activation functions.  During brain mutations a hidden node’s activation function may change to any of the types listed below.  Many of these functions are nonlinear or stateful, meaning the same input will not always produce the same output.  The table summarises the available hidden node functions, their behaviour, output range and default activation.

| Function | Behaviour and purpose | Output range | Default activation |
|---|---|---|---|
| **Sigmoid** | Logistic function `sig(x)` that smoothly caps the signal between 0 and 1.  Useful when values must remain within [0, 1], though the unactivated output of an isolated sigmoid node is 0.5. | 0 to 1 | 0.0 |
| **Linear** | Sums its inputs without transformation (`y = Σx`).  Can serve as an OR gate or simple accumulator; does not cap the magnitude. | –∞ to ∞ | 0.0 |
| **TanH** | Hyperbolic tangent `tanh(x)` of the total activation.  Provides both positive and negative outputs while tapering off at ±1.  Useful when a signal should be centred and bounded. | –1 to 1 | 0.0 |
| **Sine** | Computes `sin(x)` of the total activation.  Generates periodic responses useful for oscillatory behaviours. | –1 to 1 | 0.0 |
| **ReLu** | Rectified linear unit: returns `x` if the total activation is positive, otherwise 0.  Produces no negative output and scales linearly for positive inputs. | 0 to 1* | 0.0 |
| **Gaussian** | Returns `1/(x² + 1)` of the total activation.  A zero input yields an output near 1, while larger positive or negative activations drive the output down toward 0.  Useful for inverting signals or defining a band of activation. | 0 to 1 | 0.0 |
| **Latch** | Acts as a binary memory.  When the total activation exceeds 1, the output switches to 1; when it drops below 0, the output resets to 0; otherwise it retains its previous output.  Serves as a trigger or reset signal. | 0 to 1 | 0.0 |
| **Differential** | Outputs the rate of change of the activation rather than the activation itself.  Highlights how quickly a signal is changing (e.g., the rate at which a plant is getting closer).  Because the node’s bias is constant, the output usually appears only on the first tick after a change. | –∞ to ∞ | 0.0 |
| **Abs** | Returns the absolute value `|x|` of the total activation.  Removes sign information while preserving magnitude. | 0 to ∞ | 0.0 |
| **Mult** | Multiplies its inputs together instead of summing them.  Useful for gating behaviour: it acts like an AND gate, outputting a strong signal only when all inputs are high. | 0 to 1 | 1.0 |
| **Integrator** | Adds the current activation to its previous output (`y = y + x/Δt`) each tick.  Serves as an integrator for memory, counting or averaging.  Since it accumulates indefinitely, it can generate large positive or negative values over time. | –∞ to ∞ | 0.0 |
| **Inhibitory** | Similar to the differential but self‑inhibiting: as long as the input remains constant the output decays gradually back toward 0.  The node’s bias determines how quickly it returns to zero.  Produces acclimating behaviour. | –∞ to ∞ | 1.0 |
| **SoftLatch** | A hysteresis function.  The output tends to keep its last value, and the activation must change significantly to alter the output.  A low bias makes the node behave almost linearly; at higher bias it approximates a latch.  Useful for memory or smoothing out noise. | 0 to 1 | 5.0 |

\*ReLu nodes in the current implementation saturate at 1 because downstream signals are usually normalised, so even large positive activations rarely push the value beyond 1 in practice.

## Reproduction and Life Cycle

* Bibites reproduce **asexually** by laying eggs.  Eggs contain the offspring and any mutations; bibites must reach adulthood and have at least 50 % health to reproduce【388369820011435†L142-L160】.  Laying an egg requires investing energy to grow the offspring and cover the cost of physical traits and brain complexity【388369820011435†L149-L156】.  If mutations increase the cost beyond available energy, the hatchling may have low energy or die during development【388369820011435†L158-L160】.

* **New egg‑laying system (v0.6.0):** eggs are produced gradually and stored in the egg organ.  Bibites can lay clutches of eggs; new brain nodes `nEggStored` (input) and `EggProduction` (output) control egg production and energy investment【190337596520779†L84-L98】.  Bibites can reabsorb eggs for energy, but there is a penalty【190337596520779†L100-L101】.

* **Growth and maturity:** Genes such as **Growth Scale**, **Growth Maturity Factor** and **Growth Maturity Exponent** determine how bibites grow from hatchling to maturity.  **Size Ratio** controls adult size; a mature bibite of size ratio 1.0 measures 10 units in the game world【98074449060745†L142-L148】.

## Internal Organs and Metabolism (v0.6.0)

The **Organs and Science** update introduced seven internal organs that occupy physical area inside bibites and compete for space【190337596520779†L41-L51】.  The WAGG genes assign area to each organ, forcing trade‑offs between functions【190337596520779†L43-L50】.

| Organ | Purpose and effect | Evidence |
|------|------------------|---------|
| **Armor** | Protective layer; higher thickness increases damage resistance but adds weight【190337596520779†L53-L56】. | 【190337596520779†L53-L56】 |
| **Stomach** | Stores and digests food into energy; larger stomach can digest more simultaneously【190337596520779†L56-L58】. | 【190337596520779†L56-L58】 |
| **Egg organ (womb)** | Produces and stores eggs; larger organ allows larger clutches or more mature offspring【190337596520779†L59-L62】. | 【190337596520779†L59-L62】 |
| **Throat** | Determines mouth opening and bite size; larger throat allows swallowing bigger pellets【190337596520779†L62-L63】. | 【190337596520779†L62-L63】 |
| **Move muscles** | Powers movement and rotation; more muscle increases speed but raises energy costs【190337596520779†L64-L66】. | 【190337596520779†L64-L66】 |
| **Jaw muscles** | Used for attacking or biting; larger jaw muscles increase bite size and damage dealt【190337596520779†L67-L69】. | 【190337596520779†L67-L70】 |
| **Fat reserves** | Stores fat as a long‑term energy reserve; bibites can convert energy to fat and burn fat later.  Two genes (Fat Storage Threshold and Fat Storage Deadband) regulate when fat is stored or consumed【190337596520779†L107-L116】.  Bibites can store up to twice their base capacity; excess fat causes them to become visibly chubby【190337596520779†L118-L123】. | 【190337596520779†L107-L123】 |

* **Metabolism:** A gene (Metabolism) scales the speed of all processes.  Increasing metabolism makes bibites move, digest and process information faster, but also increases energy consumption proportionally【190337596520779†L265-L274】.  Energy usage efficiency decreases with higher metabolism【190337596520779†L265-L274】.

* **Combat and physics:** Biting and combat are now physics‑based.  Materials have cohesiveness; a bibite must apply enough force with its jaw to remove a chunk of material.  Armour must be breached before damage is inflicted【190337596520779†L233-L263】.  This system prevents small bibites from easily killing larger ones; large jaws and throats are crucial for predators【190337596520779†L245-L253】.

## Pheromones and Communication

* Pheromones are chemical signals emitted by bibites; they exist in red, green and blue channels.  Input neurons provide the angle and intensity of pheromone trails, while output neurons `PhereOut1–3` control pheromone production【141576286349584†L142-L157】.  Pheromones are social signals; isolated bibites rarely benefit from them【141576286349584†L146-L149】.  Some species evolve *pheromone trickery*, where one species uses pheromones to trigger behaviours in another species (e.g., making predators flee or causing competitors to abandon food); this is akin to Batesian mimicry【951444200227588†L149-L166】.

## Energy, Biomass and World Parameters

* **Energy forms:** Energy is stored in body points (max health), health, energy reserves, eggs, plant pellets and meat pellets【941281487284828†L144-L160】.  Actions (moving, thinking, producing eggs, biting, grabbing, producing pheromones) consume energy; inefficiencies return energy to biomass【941281487284828†L144-L150】.

* **Biomass density:** A world parameter controlling the amount of biomass energy per unit area.  The total energy in a simulation is `T_E = Biomass Density × (Simulation Size)^2`【184933498286318†L142-L155】.  High biomass density increases the initial number of plant pellets; low values make sustainable lineages less likely and evolution slower【184933498286318†L160-L177】.  Excessively high values can cause performance issues due to large populations【184933498286318†L180-L185】.

* **Simulation size:** Defines the square world’s side length (in units).  Mature bibites (size ratio 1.0) are ~10 units long【98074449060745†L142-L148】.  Bibites can wander outside the designated area, but food does not grow there【98074449060745†L150-L153】.  Larger worlds increase energy and permit geographical isolation, encouraging speciation; small worlds impose scarcity and can lead to cannibalism【98074449060745†L160-L170】.  Simulation size affects performance (doubling size quadruples area and entities)【98074449060745†L173-L177】.

* **Automatic void avoidance:** A cheat option that turns bibites away when they approach the world boundary so they do not need to evolve boundary avoidance behaviour【595071010469671†L142-L145】.

* **Virus settings:** Enabling viruses allows virions to spawn and infect bibites; spawn rate is determined by the *Virus Generation Time*【879210222847848†L142-L151】.

## Speciation, Statistics and Tracking (v0.6.0)

* **Species identification:** The simulation now automatically categorises bibites into species and tracks their lineages【190337596520779†L150-L166】.  Species are named after Patreon supporters or significant contributors by default; names can be set manually and are inherited by descendants【190337596520779†L156-L159】.  Species history can be viewed via a lineage tree showing relatedness or a lineage mesh emphasising population sizes over time【190337596520779†L164-L170】.

* **Data tracking:** Many metrics are logged at regular intervals and progressively compressed to reduce file size.  Tracked data include biomass (total energy in plants, meat and bibites), entity counts, age distribution, brain size distribution, egg‑laying statistics and gene distributions【190337596520779†L214-L229】.  The data logging system uses logarithmic sampling so resolution decreases with age, analogous to the fossil record【190337596520779†L137-L145】.  Users can “favourite” a species to prevent it being pruned from history【190337596520779†L190-L203】.

## Other Features and Tools

* **Procedural sprites:** Each bibite’s sprite is generated from its genes (e.g., body colour), so individuals look unique【332528119055686†L41-L47】.

* **Self‑awareness:** Bibites have internal senses (energy ratio, life ratio, maturity, etc.) allowing them to regulate behaviour【354801190563701†L140-L162】.

* **Engineering tools and scenarios:** Users can create custom bibites or load template bibites suited for particular environments (e.g., filter‑feeding whales, efficient camels)【5365637378740†L122-L133】.  Scenarios provide preset environmental parameters and zones; zones allow different pellet types, fertility or movement rules in different map regions【5365637378740†L88-L115】.  Users can adjust parameters during simulations, save them as scenarios and share them【5365637378740†L135-L144】.  A pellet‑placer tool allows placing piles of pellets or drawing shapes【5365637378740†L148-L156】.

## Implementation Notes for Re‑creating the Simulation

Implementing a reconstruction of The Bibites simulation (e.g., for BIOME integration) requires modelling the following components:

1. **Environment:** A square 2‑D space with boundaries (simulation size).  Biomass energy is distributed according to the *Biomass Density* parameter; plant pellets spawn by consuming biomass; meat pellets result from dead bibites; energy conversions must conserve total energy.

2. **Bibites:** Represent each bibite with:
   * A **genome**: continuous gene values controlling physical traits (colour, size ratio, diet, organ sizes, metabolism, growth and maturity parameters, mutation rates, behaviour weights, immune activation).  Genes mutate at reproduction according to the rules described above.
   * A **brain**: neural network with input, hidden and output nodes, plus synapses.  Nodes have activation functions and accumulate stimuli; synapses have strengths and can be enabled/disabled.  At each simulation step, compute input node values from sensors, propagate through the network to compute output activations, and apply resulting behaviours.
   * **Internal state**: energy reserves, biomass in organs (stomach contents, fat), health, maturity, eggs stored, internal clock counters, etc.
   * **Physics**: Position, velocity and rotation; interactions with pellets and other bibites (e.g., grabbing, throwing, biting).  Physics must handle collisions and force application (e.g., jaw force vs. material cohesion).
   * **Organs:** Each organ occupies a fraction of internal area based on WAGG genes; organs determine capacities (digestion rate, egg clutch size, bite size, movement power, fat storage, armour thickness, jaw strength).  Changing organ proportions trades off capabilities.

3. **Reproduction:** When conditions are met (adult, sufficient health), a bibite can produce an egg: create a new genome and brain by copying the parent, apply gene and brain mutations, invest required energy, and schedule growth over time.  Eggs take space and energy; parents can reabsorb eggs at a penalty.

4. **Energy and metabolism:** Implement consumption and production of energy: movement, digestion, thinking, producing pheromones, grabbing/throwing and combat cost energy.  Metabolism gene scales process rates and energy usage; inefficiencies return energy to biomass.  Fat storage and consumption should follow the threshold and deadband genes.

5. **Sensory system:** Provide the brain’s input nodes with values calculated each time step: distances and angles to resources and other bibites, internal state ratios, pheromone gradients, timer values, etc.

6. **Output actions:** Map output neuron activations to behaviours: acceleration, rotation, egg production, egg laying, eating/vomiting, digestion rate, grabbing/throwing objects, producing pheromones, growth and healing rates, biting force, etc.  Biting should be physics‑based; damage depends on applied force and target material cohesiveness.

7. **Pheromones:** When `PhereOut` neurons are active, deposit pheromones of the respective colour in the environment; pheromones diffuse or dissipate over time.  Input neurons read pheromone gradients to inform behaviour.  Consider evolving pheromone trickery by allowing pheromone signals to affect multiple species.

8. **Viruses (optional):** If virus settings are enabled, periodically spawn virions that can infect bibites; infection effects and replication rates would need to be defined.

9. **Speciation and tracking:** Maintain a tree of lineages.  Define a species based on genetic and/or brain similarity thresholds.  Log metrics (population sizes, gene distributions, brain sizes, etc.) at regular intervals and implement logarithmic compression to reduce storage.

10. **User tools and scenarios:** Provide an interface for adjusting world parameters, editing genomes and brains, saving and loading simulations, creating zones and scenarios, placing pellets, and controlling cheat options (e.g., automatic void avoidance, virus enable).

Implementing these features will approximate the current Bibites simulation and provide a framework for integrating the BIOME algorithm (which aims to support unbounded modular evolution).  The information above summarises the existing mechanics and should help guide your reconstruction.
