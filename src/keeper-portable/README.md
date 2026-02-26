
# stack-portable README
Python/FastAPI/Slack stack

---

## Prerequisites (from requirements.txt)

- slack-bolt>=1.18.0
- anthropic>=0.25.0
- httpx>=0.27.0
- python-dotenv>=1.0.0
- uvicorn>=0.29.0

---

Keeper is the client side of the Keeper-Docket pair. 

This implementation can be connected to Slack. You can pass a transcript to it via a mention in a channel and Keeper will identify assignments, make a reasonable guess at inferred dates (such as, what does "next week" mean), commit them to Docket and provide a summary that you can verify. 
