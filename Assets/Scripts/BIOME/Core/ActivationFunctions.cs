using System;
using Unity.Mathematics;

namespace BiomeBibites.BIOME
{
    /// <summary>
    /// Implements all activation functions used by BIOME nodes.
    /// Output nodes have FIXED activation functions that cannot mutate.
    /// Hidden nodes can mutate their activation function.
    /// </summary>
    public static class ActivationFunctions
    {
        /// <summary>
        /// Applies the specified activation function to compute the node's output.
        /// </summary>
        /// <param name="funcType">The activation function type</param>
        /// <param name="activation">Accumulated input (sum of weighted incoming signals)</param>
        /// <param name="bias">Node's baseline value</param>
        /// <param name="previousOutput">Previous frame's output (for stateful functions)</param>
        /// <param name="deltaTime">Time since last update (for Integrator, Inhibitory)</param>
        /// <returns>The computed output value</returns>
        public static float Apply(ActivationFunctionType funcType, float activation, float bias,
            float previousOutput = 0f, float deltaTime = 0.016f)
        {
            float x = activation + bias;

            switch (funcType)
            {
                case ActivationFunctionType.Identity:
                    return x;

                case ActivationFunctionType.Sigmoid:
                    return Sigmoid(x);

                case ActivationFunctionType.Linear:
                    return x;

                case ActivationFunctionType.TanH:
                    return TanH(x);

                case ActivationFunctionType.Sine:
                    return math.sin(x);

                case ActivationFunctionType.ReLU:
                    return math.max(0f, x);

                case ActivationFunctionType.Gaussian:
                    return Gaussian(x);

                case ActivationFunctionType.Latch:
                    return Latch(x, previousOutput);

                case ActivationFunctionType.Differential:
                    return Differential(x, previousOutput, deltaTime);

                case ActivationFunctionType.Abs:
                    return math.abs(x);

                case ActivationFunctionType.Mult:
                    // Note: Mult normally multiplies inputs together, but here we use it
                    // as a special case where bias acts as second multiplicand
                    return math.clamp(x * bias, 0f, 1f);

                case ActivationFunctionType.Integrator:
                    return Integrator(activation, previousOutput, deltaTime);

                case ActivationFunctionType.Inhibitory:
                    return Inhibitory(activation, previousOutput, bias, deltaTime);

                case ActivationFunctionType.SoftLatch:
                    return SoftLatch(x, previousOutput, bias);

                default:
                    return x;
            }
        }

        /// <summary>
        /// Sigmoid: y = 1/(1+e^-x), range [0,1]
        /// Smoothly caps the signal between 0 and 1.
        /// </summary>
        public static float Sigmoid(float x)
        {
            return 1f / (1f + math.exp(-x));
        }

        /// <summary>
        /// TanH: y = tanh(x), range [-1,1]
        /// Provides both positive and negative outputs while tapering at extremes.
        /// </summary>
        public static float TanH(float x)
        {
            return math.tanh(x);
        }

        /// <summary>
        /// Gaussian: y = 1/(x^2+1), range (0,1]
        /// Zero input yields ~1, larger inputs drive toward 0.
        /// Useful for inverting signals or defining activation bands.
        /// </summary>
        public static float Gaussian(float x)
        {
            return 1f / (x * x + 1f);
        }

        /// <summary>
        /// Latch: Binary memory.
        /// When x > 1, output switches to 1.
        /// When x < 0, output resets to 0.
        /// Otherwise, retains previous output.
        /// </summary>
        public static float Latch(float x, float previousOutput)
        {
            if (x > 1f) return 1f;
            if (x < 0f) return 0f;
            return previousOutput;
        }

        /// <summary>
        /// Differential: Rate of change.
        /// Outputs how quickly the signal is changing.
        /// </summary>
        public static float Differential(float x, float previousOutput, float deltaTime)
        {
            if (deltaTime <= 0f) return 0f;
            return (x - previousOutput) / deltaTime;
        }

        /// <summary>
        /// Integrator: Accumulator.
        /// Adds current activation to previous output.
        /// y = y_prev + x * dt
        /// </summary>
        public static float Integrator(float activation, float previousOutput, float deltaTime)
        {
            return previousOutput + activation * deltaTime;
        }

        /// <summary>
        /// Inhibitory: Self-decaying.
        /// Similar to differential but self-inhibiting.
        /// Output decays gradually back toward 0 when input is constant.
        /// Bias determines decay rate.
        /// </summary>
        public static float Inhibitory(float activation, float previousOutput, float bias, float deltaTime)
        {
            float decayRate = math.max(0.1f, bias);
            float decay = math.exp(-decayRate * deltaTime);
            return previousOutput * decay + activation * (1f - decay);
        }

        /// <summary>
        /// SoftLatch: Hysteresis function.
        /// Output tends to keep its last value; activation must change significantly to alter output.
        /// Low bias = nearly linear; high bias = approximates a latch.
        /// </summary>
        public static float SoftLatch(float x, float previousOutput, float bias)
        {
            float steepness = math.max(0.1f, bias);
            float target = Sigmoid(x * steepness);
            float blend = 1f / (1f + steepness);
            return math.lerp(previousOutput, target, blend);
        }

        /// <summary>
        /// Gets the default bias value for a given activation function type.
        /// </summary>
        public static float GetDefaultBias(ActivationFunctionType funcType)
        {
            switch (funcType)
            {
                case ActivationFunctionType.Mult:
                    return 1f;
                case ActivationFunctionType.Inhibitory:
                    return 1f;
                case ActivationFunctionType.SoftLatch:
                    return 5f;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Gets a random activation function type suitable for hidden nodes.
        /// </summary>
        public static ActivationFunctionType GetRandomHiddenFunction(ref Unity.Mathematics.Random random)
        {
            // Exclude Identity (for genes only) and weight toward common functions
            int choice = random.NextInt(0, 13);
            return choice switch
            {
                0 => ActivationFunctionType.Sigmoid,
                1 => ActivationFunctionType.Linear,
                2 => ActivationFunctionType.TanH,
                3 => ActivationFunctionType.TanH,  // Higher weight for TanH
                4 => ActivationFunctionType.Sine,
                5 => ActivationFunctionType.ReLU,
                6 => ActivationFunctionType.ReLU,  // Higher weight for ReLU
                7 => ActivationFunctionType.Gaussian,
                8 => ActivationFunctionType.Latch,
                9 => ActivationFunctionType.Differential,
                10 => ActivationFunctionType.Abs,
                11 => ActivationFunctionType.Integrator,
                12 => ActivationFunctionType.SoftLatch,
                _ => ActivationFunctionType.TanH
            };
        }
    }
}
