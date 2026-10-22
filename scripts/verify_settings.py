"""Compare .github/settings.yml against the live repository.

The declaration is the intent; GitHub is the enforced state. This script exists
so the two cannot drift apart silently: it exits non-zero and prints exactly
which field disagrees, rather than letting a declaration rot into fiction.

Differences that .github/settings.yml itself records under `known_divergences`
are reported as warnings rather than failures. Those are the ones this project
has deliberately decided to live with - a single-maintainer repository cannot
satisfy GitHub's rule that you may not approve your own pull request, so the
declared review requirement is kept as intent, not as a live setting.

Run from the repository root, with these files present:
    live-repo.json        gh api repos/$REPO
    live-protection.json  gh api repos/$REPO/branches/main/protection
    live-labels.json      gh label list --json name,color,description
"""

import json
import os
import sys

import yaml


def load_yaml(path):
    with open(path, encoding="utf-8") as handle:
        return yaml.safe_load(handle)


def main():
    dry_run = os.environ.get("DRY_RUN", "").lower() == "true"

    settings = load_yaml(".github/settings.yml")
    with open("live-repo.json", encoding="utf-8-sig") as handle:
        repo = json.load(handle)
    with open("live-protection.json", encoding="utf-8-sig") as handle:
        protection = json.load(handle)

    # `null` means the live branch-protection payload could not be read. On
    # github.com the GITHUB_TOKEN has no Administration scope, so this is what CI
    # always gets - not an accident, and not evidence of drift. Say "unverified"
    # rather than comparing the declaration against a state nobody observed.
    protection_readable = protection is not None
    protection = protection or {}
    with open("live-labels.json", encoding="utf-8-sig") as handle:
        labels = json.load(handle)

    known = set(settings.get("known_divergences") or [])
    declared_repo = settings.get("repository") or {}
    failures = []
    warnings = []

    def compare(key, declared, actual):
        if declared == actual:
            return
        if key in known:
            warnings.append(
                "{}: declared {!r}, live {!r} (recorded as a known divergence)".format(
                    key, declared, actual
                )
            )
        else:
            failures.append("{}: declared {!r}, live {!r}".format(key, declared, actual))

    unverifiable = []
    for field in (
        "description",
        "homepage",
        "has_issues",
        "has_projects",
        "has_wiki",
        "has_downloads",
        "has_discussions",
        "allow_squash_merge",
        "allow_merge_commit",
        "allow_rebase_merge",
        "delete_branch_on_merge",
        "squash_merge_commit_title",
        "squash_merge_commit_message",
    ):
        if field not in declared_repo:
            continue
        if field not in repo:
            # GitHub omits the merge and squash settings from the repository
            # payload when the caller lacks admin access, rather than returning
            # false. Comparing a declared True against an absent key reports drift
            # that does not exist, so record it as unverified instead of inventing
            # a live value for it.
            unverifiable.append(field)
            continue
        compare("repository." + field, declared_repo[field], repo.get(field))

    if "topics" in declared_repo:
        if "topics" in repo:
            compare(
                "repository.topics",
                sorted(declared_repo["topics"] or []),
                sorted(repo.get("topics") or []),
            )
        else:
            unverifiable.append("topics")

    if unverifiable:
        warnings.append(
            "repository.{}: NOT VERIFIED - the live payload does not contain these "
            "fields, so the declaration was not compared against anything. That is a "
            "token-permission gap (they need admin access), not drift. Check "
            "out-of-band: gh api repos/{}".format(
                ", ".join(unverifiable), repo.get("full_name") or "<owner>/<repo>"
            )
        )

    for branch in settings.get("branches") or []:
        name = branch.get("name", "main")
        declared_protection = branch.get("protection") or {}

        if not protection_readable:
            warnings.append(
                "{}.protection: NOT VERIFIED - the token running this check cannot "
                "read branch protection, so the declaration was not compared against "
                "anything. Check it out-of-band: gh api "
                "repos/{}/branches/{}/protection".format(
                    name, repo.get("full_name") or "<owner>/<repo>", name
                )
            )
            continue

        contexts = (protection.get("required_status_checks") or {}).get("contexts") or []
        compare(
            name + ".required_status_checks.contexts",
            sorted((declared_protection.get("required_status_checks") or {}).get("contexts") or []),
            sorted(contexts),
        )

        compare(
            name + ".required_pull_request_reviews",
            bool(declared_protection.get("required_pull_request_reviews")),
            bool(protection.get("required_pull_request_reviews")),
        )

        for field in ("enforce_admins", "allow_force_pushes", "allow_deletions"):
            if field in declared_protection:
                compare(
                    name + "." + field,
                    declared_protection.get(field),
                    (protection.get(field) or {}).get("enabled"),
                )

    live_labels = {label["name"]: label for label in labels}
    for label in settings.get("labels") or []:
        label_name = label["name"]
        actual = live_labels.get(label_name)
        if actual is None:
            message = "labels.{}: declared but missing from the live repository".format(label_name)
            # In a dry run the labels are deliberately not created, so a missing
            # one is the thing being reported, not a failure of the run.
            (warnings if dry_run else failures).append(message)
            continue
        # A label that exists but differs is exactly what the apply step fixes,
        # so in a dry run it is reported as "would change", not as a failure.
        for field, declared, live in (
            (
                "color",
                (label.get("color") or "").lower().lstrip("#"),
                (actual.get("color") or "").lower().lstrip("#"),
            ),
            ("description", label.get("description", ""), actual.get("description", "")),
        ):
            key = "labels.{}.{}".format(label_name, field)
            if declared == live:
                continue
            message = "{}: declared {!r}, live {!r}".format(key, declared, live)
            if dry_run:
                message += " (would be changed by the apply step)"
                warnings.append(message)
            else:
                failures.append(message)

    for warning in warnings:
        print("::warning::{}".format(warning))
    for failure in failures:
        print("::error::{}".format(failure))

    if failures:
        print(
            "\n{} declared field(s) disagree with the live repository.".format(len(failures)),
            file=sys.stderr,
        )
        return 1

    print(
        "No undeclared drift found ({} warning(s); see above for anything this run "
        "could not verify).".format(len(warnings))
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
