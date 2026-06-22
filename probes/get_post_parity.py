# PROBE — GET vs POST structured-envelope parity. Run from the AdventureBreaker repo:
#   AB_RUNS_DIR=runs python3 probes/get_post_parity.py
# RESULTS vs prod main 8175684:
#   moves/score/locationName/lastMovementDirection MATCH across GET and POST.
#   previousLocationName: POST="Behind House", GET=null  -> BUG, filed zork#250
#   (engine-only field set during a turn, not stored on Context -> dropped on no-turn GET rehydrate).
#   Related: #230 (inventory, fixed), #238 (dark exits/actions).
import json
from adventurebreaker import config
from adventurebreaker.client import ApiClient
from adventurebreaker.ledger import Run
run = Run.load("parity")                      # or: harness new --game zork --name parity
c = config.resolve(run.game, run.target)
cli = ApiClient(c["base_url"], c["endpoint"])
keys = ("locationName","previousLocationName","lastMovementDirection","moves","score","time")
f = lambda txt: {k: json.loads(txt).get(k) for k in keys}
print("POST:", f(cli.play(run.session_id, "E", narrator=False).raw_text))
print("GET :", f(cli.init(run.session_id).raw_text))
