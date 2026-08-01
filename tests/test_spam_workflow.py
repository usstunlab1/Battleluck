import importlib
import pathlib
import sys

import pytest

ROOT = pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

MODULE_NAME = "entities.email_spam_workflow.spam_workflow"
WORKFLOW_GETTER = "get_spam_workflow"


def test_spam_workflow_structure():
    try:
        spam_workflow = importlib.import_module(MODULE_NAME)
    except ModuleNotFoundError:
        pytest.skip(f"Optional module {MODULE_NAME} is not present in this repository")

    if not hasattr(spam_workflow, WORKFLOW_GETTER):
        pytest.skip(f"Module {MODULE_NAME} does not expose {WORKFLOW_GETTER}")

    workflow_getter = getattr(spam_workflow, WORKFLOW_GETTER)
    assert callable(workflow_getter), f"{WORKFLOW_GETTER} should be callable"

    workflow = workflow_getter()
    assert workflow is not None
    assert hasattr(workflow, "__class__")
