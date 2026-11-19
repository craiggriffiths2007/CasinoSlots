mergeInto(LibraryManager.library, {
    CandyCasinoBuyFreeSpins: function (gameObjectNamePtr, bet, requestIdPtr, accountIdPtr, tokenPtr) {
        window.CandyCasinoSpin(UTF8ToString(gameObjectNamePtr), bet,
            UTF8ToString(requestIdPtr), UTF8ToString(accountIdPtr), UTF8ToString(tokenPtr), true);
    },

    CandyLoadCasinoAccount: function (gameObjectNamePtr) {

        const gameObjectName =
            UTF8ToString(gameObjectNamePtr);

        window.CandyLoadCasinoAccount(
            gameObjectName);
    },

    CandyCasinoSpin: function (
        gameObjectNamePtr,
        bet,
        requestIdPtr,
        accountIdPtr,
        tokenPtr) {

        const gameObjectName =
            UTF8ToString(gameObjectNamePtr);

        const requestId =
            UTF8ToString(requestIdPtr);

        const accountId =
            UTF8ToString(accountIdPtr);

        const token =
            UTF8ToString(tokenPtr);

        window.CandyCasinoSpin(
            gameObjectName,
            bet,
            requestId,
            accountId,
            token);
    }
});
