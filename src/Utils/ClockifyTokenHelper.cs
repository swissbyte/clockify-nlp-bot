﻿using System.Threading;
using System.Threading.Tasks;
using Bot.States;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;

namespace Bot.Utils
{
    public interface IUserProfileHelper
    {
        public Task<UserProfile> GetUserProfileFromUserStateAsync(UserState userState, ITurnContext context,
            CancellationToken cancellationToken);
    }

    // We need a non-static class in order to mock and test
    internal class UserProfileHelper : IUserProfileHelper
    {
        public async Task<UserProfile> GetUserProfileFromUserStateAsync(UserState userState,
            ITurnContext context,
            CancellationToken cancellationToken)
        {
            return await userState.CreateProperty<UserProfile>("UserProfile")
                .GetAsync(context, () => new UserProfile(), cancellationToken);
        }
    }

    public static class StaticUserProfileHelper
    {
        public static IUserProfileHelper UserProfileHelper = new UserProfileHelper();

        public static Task<UserProfile> GetUserProfileAsync(UserState userState,
            ITurnContext context,
            CancellationToken cancellationToken)
        {
            return UserProfileHelper.GetUserProfileFromUserStateAsync(userState, context, cancellationToken);
        }
    }
}