mergeInto(LibraryManager.library, {
    PhoenixBalanceUpdated: function (balancePtr) {
        window.parent.postMessage({
            type: "phoenix-slot-2-balance",
            balance: UTF8ToString(balancePtr)
        }, window.location.origin);
    }
});
