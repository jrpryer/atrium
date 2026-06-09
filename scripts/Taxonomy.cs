using Godot;
using System;

namespace PuzzleEngine {
	/// <summary>
	/// TaxonomyResource defines the shared world ontology used by all lenses.
	/// </summary>
	public class TaxonomyResource : Resource {
		[Export] public string DomainName;
		[Export] public float[] SubTraits; // e.g., "sigil", "era", etc.
		[Export] public int[] Eras;
	}

	/// <summary>
	/// LensResource defines the axis each locale reads, plus its gating instrument.
	/// </summary>
	public class LensResource : Resource {
		[Export] public string Id; // e.g., "library_shelf_key"
		[Export] public string Axis; // what it reads (e.g., "classification")
		[Export] string Grammar; // how the player learns to use this lens
		[Export] Resource GatingInstrument; // e.g., field guide / shelf-sigil key
	}

	/// <summary>
	/// MysteryObjectResource defines per-lens readings for each object.
	/// </summary>
	public class MysteryObjectResource : Resource {
		[Export] public string Id; // unique identifier
		[Export] public TaxonomyResource[] Readings; // per lens
		[Export] public ClaimResource TrueClaim; // the correct claim
		[Export] public ClaimResource[] PlausibleWrongClaims; // ≤3 cap enforced at load
		[Export] public string[] CrossReferences; // citations to other content
	}

	/// <summary>
	/// ClaimResource defines a synthesis target (combination of evidence).
	/// </summary>
	public class ClaimResource : Resource {
		[Export] public string Id; // unique identifier
		[Export] public MysteryObjectResource[] RequiredEvidence; // combination of evidence
	}

	/// <summary>
	/// StoryBeatResource defines narrative unlocked when a claim resolves.
	/// </summary>
	public class StoryBeatResource : Resource {
		[Export] public string Id; // unique identifier
		[Export] public string[] NarrativeContent; // unlockable story beat
	}

	/// <summary>
	/// EvidenceLedger tracks which readings the player has surfaced per object.
	/// </summary>
	public class EvidenceLedger {
		private Dictionary<string, HashSet<string>> _evidence = new Dictionary<string, HashSet<string>>();

		public void AddEvidence(string objectId, string readingId) {
			if (!_evidence.ContainsKey(objectId)) {
				_evidence[objectId] = new HashSet<string>();
			}
			_evidence[objectId].Add(readingId);
		}

		public bool HasEvidence(string objectId, string readingId) {
			return _evidence.ContainsKey(objectId) && _evidence[objectId].Contains(readingId);
		}

		public void RemoveEvidence(string objectId, string readingId) {
			if (_evidence.ContainsKey(objectId)) {
				_evidence[objectId].Remove(readingId);
			}
		}

		public bool IsComplete(string objectId) {
			return _evidence.ContainsKey(objectId);
		}
	}

	/// <summary>
	/// ComprehensionGate checks if the player has surfaced required readings before attempting a claim.
	/// </summary>
	public class ComprehensionGate {
		private EvidenceLedger _ledger; // injected dependency

		public bool IsComprehensible(MysteryObjectResource obj, ClaimResource claim) {
			foreach (var evidence in claim.RequiredEvidence) {
				if (!_ledger.HasEvidence(obj.Id, evidence.Id)) {
					return false; // not yet comprehensible
				}
			}
			return true; // all required readings surfaced
		}
	}

	/// <summary>
	/// SynthesisResolver evaluates a permitted claim against {true | plausible-wrong | invalid}.
	/// </summary>
	public class SynthesisResolver {
		private EvidenceLedger _ledger; // injected dependency

		public ClaimResult EvaluateClaim(MysteryObjectResource obj, ClaimResource proposedClaim) {
			if (proposedClaim == obj.TrueClaim) {
				return new ClaimResult { Status = SynthesisStatus.Resolved };
			} else if (obj.PlausibleWrongClaims.Contains(proposedClaim)) {
				return new ClaimResult { Status = SynthesisStatus.NearMiss, Discrepancy = "unique_discrepancy" };
			} else {
				return new ClaimResult { Status = SynthesisStatus.Invalid };
			}
		}
	}

	/// <summary>
	/// WorldClock drives time-based transformations (Spirit Pools).
	/// </summary>
	public class WorldClock {
		private float _time; // slow real-time tick
		private bool _paused; // pausable

		public float GetTime() {
			return _time;
		}

		public void Update(float delta) {
			if (!_paused) {
				_time += delta * 0.1f; // slow clock
			}
		}

		public void Pause() {
			_paused = true;
		}

		public void Resume() {
			_paused = false;
		}
	}

	/// <summary>
	/// UnresolvedRegistry tracks open objects and re-surfaces them when a new lens is opened.
	/// </summary>
	public class UnresolvedRegistry {
		private HashSet<string> _openObjects; // set of open objects

		public void AddObject(string objectId) {
			_openObjects.Add(objectId);
		}

		public void RemoveObject(string objectId) {
			_openObjects.Remove(objectId);
		}

		public bool IsOpen(string objectId) {
			return _openObjects.Contains(objectId);
		}

		public void Resurface() {
			// re-surface open objects when a new lens is opened
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
