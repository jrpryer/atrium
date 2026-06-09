using Godot;
using System;

namespace PuzzleEngine {
	/// <summary>
	/// Core puzzle engine that manages the lens/evidence/synthesis model.
	/// </summary>
	public static class PuzzleEngine {
		private static EvidenceLedger _evidenceLedger = new EvidenceLedger();
		private static ComprehensionGate _comprehensionGate = new ComprehensionGate();
		private static SynthesisResolver _synthesisResolver = new SynthesisResolver();
		private static WorldClock _worldClock = new WorldClock();
		private static UnresolvedRegistry _unresolvedRegistry = new UnresolvedRegistry();

		public static EvidenceLedger EvidenceLedget => _evidenceLedger;
		public static ComprehensionGate ComprehensionGate => _comprehensionGate;
		public static SynthesisResolver SynthesisResolver => _synthesisResolver;
		public static WorldClock WorldClock => _worldClock;
		public static UnresolvedRegistry UnresolvedRegistry => _unresolvedRegistry;

		/// <summary>
		/// Validates that the ≤3 cap on plausible-wrong claims is enforced.
		/// </summary>
		public static bool ValidateCapAtLoad(MysteryObjectResource obj) {
			if (obj.PlausibleWrongClaims.Count > 3) {
				GD.Print("ERROR: Mystery object exceeds ≤3 cap on plausible-wrong claims.");
				return false;
			}
			return true;
		}

		/// <summary>
		/// Processes a claim attempt through the comprehension gate and synthesis resolver.
		/// </summary>
		public static ClaimResult ProcessClaimAttempt(MysteryObjectResource obj, ClaimResource proposedClaim) {
			if (!_comprehensionGate.IsComprehensible(obj, proposedClaim)) {
				return new ClaimResult { Status = SynthesisStatus.NotYetComprehensible };
			}

			var result = _synthesisResolver.EvaluateClaim(obj, proposedClaim);
			if (result.Status == SynthesisStatus.Resolved) {
				// Unlock story beat
				UnlockStoryBeat(result.StoryBeatId);
			} else if (result.Status == SynthesisStatus.NearMiss) {
				// Track near-miss discrepancy
				TrackNearMiss(obj, proposedClaim);
			}

			return result;
	 }

		private static void UnlockStoryBeat(string storyBeatId) {
			// Implementation for unlocking story beats
		}

		private static void TrackNearMiss(MysteryObjectResource obj, ClaimResource claim) {
			// Implementation for tracking near-miss discrepancies
		}
	}

	public enum SynthesisStatus {
		NotYetComprehensible,
		Resolved,
		NearMiss,
		Invalid
	}

	public class ClaimResult {
		public SynthesisStatus Status;
		public string StoryBeatId;
		public string Discrepancy;
	}
}
