"""Validate the GitHub Actions workflow files against GitHub's own schema rules.

WHY THIS EXISTS
---------------
PyYAML does not know which keys a workflow step is allowed to have. A key at
the wrong indentation is still valid YAML, so `yaml.safe_load` accepts it
silently - but GitHub validates workflow files against a schema and rejects
anything it does not recognise.

That is not theoretical. `.github/workflows/apply-repo-settings.yml` carried a
mis-indented `REPO:` key at STEP level instead of inside `env:`, so the whole
file was invalid. GitHub reports an invalid workflow as a red run with ZERO
jobs and ignores its `paths:` filter, so the workflow failed on every push -
including pushes that never touched the file it was supposed to react to - and
nobody noticed, because it is not a required status check. The first symptom
was a green run whose `name:` came back as the file path rather than the
declared name.

PyYAML says the file is fine. Only GitHub's schema says otherwise, and GitHub
only says so after a push, as a run with no jobs. This check says it before.

WHAT IS CHECKED
---------------
* Every key on a step is one GitHub accepts: name, id, if, uses, run, shell,
  with, env, working-directory, continue-on-error, timeout-minutes.
* Every key on a job is one GitHub accepts: name, needs, runs-on, permissions,
  environment, concurrency, outputs, env, defaults, if, steps, timeout-minutes,
  strategy, continue-on-error, container, services, uses, with, secrets.
* Every key at workflow level is one GitHub accepts: name, run-name, on, env,
  defaults, concurrency, permissions, jobs.
* A key inside `env:` is indented deeper than the `env:` it belongs to. This is
  the specific defect above, caught directly rather than inferred.

Run from the repository root:  python scripts/validate_workflows.py
"""

import glob
import os
import sys

import yaml

WORKFLOW_KEYS = {
    "name",
    "run-name",
    "on",
    "env",
    "defaults",
    "concurrency",
    "permissions",
    "jobs",
}
JOB_KEYS = {
    "name",
    "needs",
    "runs-on",
    "permissions",
    "environment",
    "concurrency",
    "outputs",
    "env",
    "defaults",
    "if",
    "steps",
    "timeout-minutes",
    "strategy",
    "continue-on-error",
    "container",
    "services",
    "uses",
    "with",
    "secrets",
}

STEP_KEYS = {
    "name",
    "id",
    "if",
    "uses",
    "run",
    "shell",
    "with",
    "env",
    "working-directory",
    "continue-on-error",
    "timeout-minutes",
}

# Keys that are valid inside an `env:` mapping. Env var names are arbitrary -
# there is no allowlist - so this set is deliberately empty and the env check
# lives in check_indentation(), which catches the mis-nesting directly.


class _Loader(yaml.SafeLoader):
    """SafeLoader that rejects duplicate keys."""


def _construct_mapping(loader, node, deep=False):
    mapping = {}
    for key_node, value_node in node.value:
        key = loader.construct_object(key_node, deep=deep)
        if key in mapping:
            raise yaml.constructor.ConstructorError(
                "while constructing a mapping",
                node.start_mark,
                "found duplicate key: {!r}".format(key),
                key_node.start_mark,
            )
        mapping[key] = loader.construct_object(value_node, deep=deep)
    return mapping


_Loader.add_constructor(
    yaml.resolver.BaseResolver.DEFAULT_MAPPING_TAG, _construct_mapping
)


def check_file(path):
    """Return a list of problem strings for one workflow file."""
    problems = []
    with open(path, encoding="utf-8") as handle:
        text = handle.read()

    try:
        data = yaml.load(text, Loader=_Loader)
    except yaml.YAMLError as exc:
        return ["{}: not valid YAML: {}".format(path, exc)]

    if not isinstance(data, dict):
        return ["{}: top level is not a mapping".format(path)]

    for key in data:
        # PyYAML resolves the bare key `on:` to the boolean True (YAML 1.1).
        # GitHub resolves it to the string "on". Treat them as the same key.
        if key is True:
            continue
        if key not in WORKFLOW_KEYS:
            problems.append(
                "{}: unknown workflow-level key {!r}".format(path, key)
            )

    jobs = data.get("jobs") or {}
    if not isinstance(jobs, dict):
        problems.append("{}: jobs is not a mapping".format(path))
        return problems

    for job_name, job in jobs.items():
        if not isinstance(job, dict):
            problems.append(
                "{}: job {!r} is not a mapping".format(path, job_name)
            )
            continue

        for key in job:
            if key not in JOB_KEYS:
                problems.append(
                    "{}: job {!r} has unknown key {!r}".format(path, job_name, key)
                )

        steps = job.get("steps") or []
        if not isinstance(steps, list):
            problems.append(
                "{}: job {!r} steps is not a list".format(path, job_name)
            )
            continue

        for index, step in enumerate(steps):
            if not isinstance(step, dict):
                problems.append(
                    "{}: job {!r} step {} is not a mapping".format(
                        path, job_name, index
                    )
                )
                continue

            for key in step:
                if key not in STEP_KEYS:
                    problems.append(
                        "{}: job {!r} step {} has unknown key {!r} "
                        "(a step accepts only {})".format(
                            path, job_name, index, key, ", ".join(sorted(STEP_KEYS))
                        )
                    )

            # An env: mapping whose entries are not indented under it is the
            # defect that made apply-repo-settings.yml invalid, and it is the
            # one that survives a plain YAML parse. check_indentation() finds
            # it directly; here we only report it if it somehow got through.
            env = step.get("env")
            if isinstance(env, dict) and not env:
                problems.append(
                    "{}: job {!r} step {} has an empty env: block".format(
                        path, job_name, index
                    )
                )

    return problems


def check_indentation(path):
    """Catch an `env:` entry that sits at the same level as the `env:` itself."""
    problems = []
    with open(path, encoding="utf-8") as handle:
        lines = handle.read().split("\n")

    for number, line in enumerate(lines, start=1):
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            continue
        indent = len(line) - len(line.lstrip())

        if stripped == "env:":
            # The next non-blank, non-comment line must be deeper than env:
            # itself. A key at the same level is a SIBLING of env:, not a
            # child, and GitHub rejects it as an unknown step key.
            for follow in lines[number:]:
                follow_stripped = follow.strip()
                if not follow_stripped or follow_stripped.startswith("#"):
                    continue
                follow_indent = len(follow) - len(follow.lstrip())
                if follow_indent <= indent:
                    problems.append(
                        "{}:{}: {!r} is at the same indentation as its `env:` "
                        "block and is therefore a sibling key, not an "
                        "environment variable".format(
                            path, number + 1, follow_stripped.split(":")[0]
                        )
                    )
                break

    return problems


def main():
    patterns = [
        os.path.join(".github", "workflows", "*.yml"),
        os.path.join(".github", "workflows", "*.yaml"),
    ]
    files = []
    for pattern in patterns:
        files.extend(sorted(glob.glob(pattern)))

    if not files:
        print("::error::no workflow files found to validate")
        return 1

    problems = []
    for path in files:
        problems.extend(check_file(path))
        problems.extend(check_indentation(path))

    for problem in problems:
        print("::error::{}".format(problem))

    if problems:
        print(
            "\n{} workflow problem(s) found in {} file(s).".format(
                len(problems), len(files)
            ),
            file=sys.stderr,
        )
        return 1

    print("{} workflow file(s) valid against GitHub's step/job/workflow schema.".format(len(files)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
